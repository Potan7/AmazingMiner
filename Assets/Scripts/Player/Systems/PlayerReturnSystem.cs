using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using CoreDriller.Player.StatSystem;
using UnityEngine.SceneManagement;
using CoreDriller.Map;
using CoreDriller.Motion;
using CoreDriller.Map.Rendering;

namespace CoreDriller.Player
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerUpdateSystem))]
    public partial struct PlayerReturnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerMovementData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 1. 연료 고갈 감지 단계: CurrentFuel <= 0 인 엔티티를 찾아서 ForcedReturnTag 추가
            foreach (var (movementData, entity) in SystemAPI.Query<RefRO<PlayerMovementData>>()
                         .WithNone<ForcedReturnTag>()
                         .WithNone<NormalReturnTag>()
                         .WithEntityAccess())
            {
                if (movementData.ValueRO.CurrentFuel <= 0f)
                {
                    ecb.AddComponent<ForcedReturnTag>(entity);
                    Debug.LogWarning("[PlayerReturnSystem] 플레이어 연료 고갈! 강제 귀환 시퀀스를 시작합니다.");
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // 2. 귀환 시퀀스 처리 단계 (강제 귀환 또는 일반 복귀)
            ProcessReturn(ref state);
        }

        private void ProcessReturn(ref SystemState state)
        {
            Entity statEntity = Entity.Null;
            bool isForced = false;

            if (SystemAPI.TryGetSingletonEntity<ForcedReturnTag>(out var forcedEntity))
            {
                statEntity = forcedEntity;
                isForced = true;
            }
            else if (SystemAPI.TryGetSingletonEntity<NormalReturnTag>(out var normalEntity))
            {
                statEntity = normalEntity;
                isForced = false;
            }

            if (statEntity == Entity.Null) return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 2.1. 강제 귀환일 경우 인벤토리 패널티 적용
            if (isForced)
            {
                ApplyInventoryPenalty(ref state, statEntity);
            }

            // 2.2. 플레이어 연료 리필 (MaxFuel 완충 처리)
            if (SystemAPI.HasComponent<PlayerMovementData>(statEntity))
            {
                var movement = SystemAPI.GetComponentRW<PlayerMovementData>(statEntity);
                movement.ValueRW.CurrentFuel = movement.ValueRO.MaxFuel;
                Debug.Log($"[PlayerReturnSystem] 플레이어 연료 리필 완료 (양: {movement.ValueRO.MaxFuel})");
            }

            // 2.3. 귀환 상태 태그 해제
            if (isForced)
            {
                ecb.RemoveComponent<ForcedReturnTag>(statEntity);
            }
            else
            {
                ecb.RemoveComponent<NormalReturnTag>(statEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // 2.4. 휘발성 엔티티 및 Box2D 물리 리소스 정리 (메모리 누수 방지)
            CleanupPhysicsAndVolatileEntities(ref state);

            // 2.5. 귀환 직전 — ECS 현재 스탯을 PlayerManager(DontDestroyOnLoad)에 저장
            //      (씬 언로드 시 Entity가 파괴되기 전에 반드시 호출)
            PlayerManager.Instance?.SaveStatsFromEntity();

            // 2.6. HomeScene으로 씬 전환
            Debug.Log($"[PlayerReturnSystem] 귀환 시퀀스 완료 (강제여부: {isForced}). HomeScene으로 이동합니다.");
            SceneManager.LoadScene("HomeScene");

        }

        private void ApplyInventoryPenalty(ref SystemState state, Entity statEntity)
        {
            if (SystemAPI.HasBuffer<InventoryBuffer>(statEntity))
            {
                var inventory = SystemAPI.GetBuffer<InventoryBuffer>(statEntity);
                
                if (inventory.Length > 0)
                {
                    // 수집품 리스트 복사 및 가치 산정
                    var items = new NativeList<SortableItem>(inventory.Length, Allocator.Temp);
                    for (int i = 0; i < inventory.Length; i++)
                    {
                        int type = inventory[i].ItemType;
                        int count = inventory[i].Count;
                        int value = GetItemValue(type);
                        
                        items.Add(new SortableItem
                        {
                            BufferIndex = i,
                            ItemType = type,
                            Count = count,
                            Value = value
                        });
                    }

                    // 버블 정렬 (NativeList의 심플한 소팅, Burst 호환)
                    for (int i = 0; i < items.Length - 1; i++)
                    {
                        for (int j = i + 1; j < items.Length; j++)
                        {
                            if (items[i].Value < items[j].Value)
                            {
                                var temp = items[i];
                                items[i] = items[j];
                                items[j] = temp;
                            }
                        }
                    }

                    // 최상위 고급 아이템 1~2개 유실 (50% 삭감)
                    int penalizeCount = math.min(items.Length, 2);
                    for (int p = 0; p < penalizeCount; p++)
                    {
                        var target = items[p];
                        if (target.Count > 0 && target.Value > 0)
                        {
                            int lostCount = target.Count / 2; // 절반 분실
                            int remainingCount = target.Count - lostCount;

                            // 실제 버퍼 갱신
                            var originElement = inventory[target.BufferIndex];
                            if (remainingCount <= 0)
                            {
                                originElement.ItemType = 0;
                                originElement.Count = 0;
                            }
                            else
                            {
                                originElement.Count = remainingCount;
                            }
                            inventory[target.BufferIndex] = originElement;

                            Debug.LogWarning($"[ForcedReturn Penalty] 가장 가치 있는 광물 유실! 종류: {GetItemName(target.ItemType)}, 기존: {target.Count}개 -> {lostCount}개 유실 -> 남은 수량: {remainingCount}개");
                        }
                    }

                    items.Dispose();
                }
            }
        }

        private void CleanupPhysicsAndVolatileEntities(ref SystemState state)
        {
            var em = state.EntityManager;

            // 1. ChunkPhysicsBody를 가진 모든 엔티티의 Box2D 바디 파괴 및 엔티티 파괴
            using (var chunksQuery = em.CreateEntityQuery(typeof(ChunkPhysicsBody)))
            {
                var chunkBodies = chunksQuery.ToComponentDataArray<ChunkPhysicsBody>(Allocator.Temp);
                for (int i = 0; i < chunkBodies.Length; i++)
                {
                    var body = chunkBodies[i].Body;
                    if (body.isValid)
                    {
                        body.Destroy();
                    }
                }
            }
            using (var chunkEntitiesQuery = em.CreateEntityQuery(typeof(ChunkComponent)))
            {
                em.DestroyEntity(chunkEntitiesQuery);
            }

            // 2. Debris의 Box2D 물리 바디 파괴 및 엔티티 파괴
            using (var debrisQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<DebrisTag>(),
                ComponentType.ReadOnly<PhysicsBodyHandle>()))
            {
                var debrisBodies = debrisQuery.ToComponentDataArray<PhysicsBodyHandle>(Allocator.Temp);
                for (int i = 0; i < debrisBodies.Length; i++)
                {
                    var body = debrisBodies[i].Body;
                    if (body.isValid)
                    {
                        body.Destroy();
                    }
                }
            }
            using (var debrisEntitiesQuery = em.CreateEntityQuery(typeof(DebrisTag)))
            {
                em.DestroyEntity(debrisEntitiesQuery);
            }

            // 3. 데칼 엔티티 파괴
            using (var decalQuery = em.CreateEntityQuery(typeof(DamageDecalTag)))
            {
                em.DestroyEntity(decalQuery);
            }

            // 4. MapConfigData 엔티티 내에 임시 저장된 런타임 생성 커스텀 파편 프리팹 파괴 및 버퍼 클리어
            if (SystemAPI.TryGetSingletonEntity<MapConfigData>(out var configEntity))
            {
                if (em.HasBuffer<ItemDebrisPrefabElement>(configEntity))
                {
                    var buffer = em.GetBuffer<ItemDebrisPrefabElement>(configEntity);
                    
                    // 1. 임시 리스트에 프리팹 엔티티 복사
                    var prefabsToDestroy = new NativeList<Entity>(buffer.Length, Allocator.Temp);
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        prefabsToDestroy.Add(buffer[i].DebrisPrefab);
                    }

                    // 2. 버퍼는 즉시 클리어 (구조적 변경 전에 수행하면 안전)
                    buffer.Clear();

                    // 3. 복사해둔 엔티티들을 루프 돌며 안전하게 파괴 (구조적 변경 발생)
                    for (int i = 0; i < prefabsToDestroy.Length; i++)
                    {
                        var debrisPrefab = prefabsToDestroy[i];
                        if (debrisPrefab != Entity.Null && em.Exists(debrisPrefab))
                        {
                            if (em.HasComponent<PhysicsBodyHandle>(debrisPrefab))
                            {
                                var bodyHandle = em.GetComponentData<PhysicsBodyHandle>(debrisPrefab);
                                if (bodyHandle.Body.isValid)
                                {
                                    bodyHandle.Body.Destroy();
                                }
                            }
                            em.DestroyEntity(debrisPrefab);
                        }
                    }
                    prefabsToDestroy.Dispose();
                }
            }

            // 5. 플레이어의 Box2D 물리 바디 파괴 (플레이어 엔티티는 씬 언로드 시 파괴되지만 물리 엔진 바디는 즉시 해제 필요)
            if (SystemAPI.TryGetSingletonEntity<PlayerTag>(out var playerEntity))
            {
                if (em.HasComponent<PhysicsBodyHandle>(playerEntity))
                {
                    var bodyHandle = em.GetComponentData<PhysicsBodyHandle>(playerEntity);
                    if (bodyHandle.Body.isValid)
                    {
                        bodyHandle.Body.Destroy();
                        Debug.Log("[PlayerReturnSystem] 플레이어 물리 바디 파괴 완료.");
                    }
                }
            }

            Debug.Log("[PlayerReturnSystem] 휘발성 지하 월드 및 물리 리소스 정리 완료.");
        }

        private int GetItemValue(int itemType)
        {
            if (!SystemAPI.TryGetSingleton<ItemDatabaseReference>(out var itemBlobEntity))
            {
                return 0; // 아직 Addressables 로딩이 끝나지 않았다면 임시로 0 가치 반환
            }
            ref var itemBlob = ref itemBlobEntity.Reference.Value;

            for (int i = 0; i < itemBlob.Items.Length; i++)
            {
                var item = itemBlob.Items[i];
                if (item.ItemID == itemType)
                {
                    return item.Value;
                }
            }
            return 0;
        }

        private static string GetItemName(int itemType)
        {
            return itemType switch
            {
                Map.BlockTypes.Stone => "Abyssite (T5 심연석)",
                Map.BlockTypes.Gold => "Gold (T4 금광석)",
                Map.BlockTypes.Iron => "Iron (T3 철광석)",
                Map.BlockTypes.Copper => "Copper (T3 구리광석)",
                Map.BlockTypes.Coal => "Coal (T2 석탄)",
                Map.BlockTypes.Dirt => "Dirt (T1 흙)",
                _ => "Unknown"
            };
        }

        private struct SortableItem
        {
            public int BufferIndex;
            public int ItemType;
            public int Count;
            public int Value;
        }
    }
}

