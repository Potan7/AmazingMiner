using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using CoreDriller.Player.StatSystem;
using CoreDriller.Motion;

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

            // 2. 강제 귀환 시퀀스 처리 단계
            ProcessForcedReturn(ref state);
        }

        private void ProcessForcedReturn(ref SystemState state)
        {
            // ForcedReturnTag가 붙은 스탯 엔티티가 없다면 스킵
            if (!SystemAPI.TryGetSingletonEntity<ForcedReturnTag>(out var statEntity)) return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 2.1. 플레이어 위치 및 물리 좌표 리셋
            if (SystemAPI.TryGetSingletonEntity<PlayerTag>(out var playerEntity))
            {
                // 지상의 안전한 리스폰 좌표 (0, 3, 0)
                float3 spawnPos = new float3(0f, 3f, 0f);

                if (SystemAPI.HasComponent<LocalTransform>(playerEntity))
                {
                    var trans = SystemAPI.GetComponentRW<LocalTransform>(playerEntity);
                    trans.ValueRW.Position = spawnPos;
                }

                // 물리 엔진 바디 좌표 및 속도 초기화 (LowLevelPhysics2D 충돌 충격 방지)
                if (SystemAPI.HasComponent<PhysicsBodyHandle>(playerEntity))
                {
                    var bodyHandle = SystemAPI.GetComponentRW<PhysicsBodyHandle>(playerEntity);
                    if (bodyHandle.ValueRO.Body.isValid)
                    {
                        bodyHandle.ValueRW.Body.position = spawnPos.xy;
                        bodyHandle.ValueRW.Body.linearVelocity = Vector2.zero;
                    }
                }
            }

            // 2.2. 인벤토리 수집 광물 가치 내림차순 정렬 및 최상위 자원 50% 유실 패널티 적용
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
                            originElement.Count = remainingCount;
                            inventory[target.BufferIndex] = originElement;

                            Debug.LogWarning($"[ForcedReturn Penalty] 가장 가치 있는 광물 유실! 종류: {GetItemName(target.ItemType)}, 기존: {target.Count}개 -> {lostCount}개 유실 -> 남은 수량: {remainingCount}개");
                        }
                    }

                    items.Dispose();
                }
            }

            // 2.3. 플레이어 연료 리필 (MaxFuel 완충 처리)
            if (SystemAPI.HasComponent<PlayerMovementData>(statEntity))
            {
                var movement = SystemAPI.GetComponentRW<PlayerMovementData>(statEntity);
                movement.ValueRW.CurrentFuel = movement.ValueRO.MaxFuel;
                Debug.Log($"[ForcedReturn] 플레이어 연료 리필 완료 (양: {movement.ValueRO.MaxFuel})");
            }

            // 2.4. 귀환 상태 태그 해제
            ecb.RemoveComponent<ForcedReturnTag>(statEntity);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static int GetItemValue(int itemType)
        {
            return itemType switch
            {
                Map.BlockTypes.Abyssite => 100, // T5
                Map.BlockTypes.Gold => 80,      // T4
                Map.BlockTypes.Iron => 50,      // T3
                Map.BlockTypes.Copper => 45,    // T3
                Map.BlockTypes.Coal => 20,      // T2
                Map.BlockTypes.Dirt => 5,       // T1
                _ => 0
            };
        }

        private static string GetItemName(int itemType)
        {
            return itemType switch
            {
                Map.BlockTypes.Abyssite => "Abyssite (T5 심연석)",
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
