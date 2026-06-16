using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.LowLevelPhysics2D;
using CoreDriller.Player.StatSystem;
using CoreDriller.Motion;
using CoreDriller.Map;

namespace CoreDriller.Player.ItemPickup
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsHandleSystem))]
    [BurstCompile]
    public partial struct PlayerItemPickupSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerInventoryData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // 1. 싱글톤 정보들 및 플레이어 물리 상태 조회
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var statEntity = SystemAPI.GetSingletonEntity<PlayerInventoryData>();

            if (!SystemAPI.HasComponent<LocalTransform>(playerEntity) || 
                !SystemAPI.HasBuffer<InventoryBuffer>(statEntity))
            {
                ecb.Dispose();
                return;
            }

            var playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
            var inventoryData = SystemAPI.GetComponent<PlayerInventoryData>(statEntity);
            var inventory = SystemAPI.GetBuffer<InventoryBuffer>(statEntity);

            // 업그레이드 등으로 슬롯 카운트가 커졌을 때 버퍼 자동 확장
            if (inventory.Length < inventoryData.InventorySlotCount)
            {
                while (inventory.Length < inventoryData.InventorySlotCount)
                {
                    inventory.Add(new InventoryBuffer { ItemType = 0, Count = 0 });
                }
            }

            float3 playerPos = playerTransform.Position;
            float pickupRange = inventoryData.ItemPickupRange;
            int inventorySize = inventoryData.InventorySlotCount;
            int slotSize = inventoryData.InventorySlotSize;
            float dt = SystemAPI.Time.DeltaTime;
            double elapsedTime = SystemAPI.Time.ElapsedTime;

            // 2. 자석 인력 및 디스폰/획득 처리를 위해 잡 실행
            // 구조 변경(ECB 및 DynamicBuffer 수정)이 일어나므로 싱글 스레드 혹은 ParallelWriter가 편리하지만, 
            // 인벤토리 누적 로직이 DynamicBuffer를 순회하고 수정해야 하므로 안전하게 메인 스레드 Job 또는 Sequential Execute로 수행합니다.
            foreach (var (debrisComp, bodyHandle, transform, entity) in SystemAPI.Query<RefRO<DebrisComponent>, RefRW<PhysicsBodyHandle>, RefRO<LocalTransform>>()
                         .WithAll<DebrisTag>()
                         .WithEntityAccess())
            {
                var body = bodyHandle.ValueRO.Body;
                if (!body.isValid) continue;

                float2 debrisPos = body.position;
                float2 diff = playerPos.xy - debrisPos;
                float dist = math.length(diff);
                int itemType = debrisComp.ValueRO.ItemType;

                // A. 수명 초과 자연 디스폰 처리 (메모리 누수 방지를 위해 물리 바디 반드시 파괴)
                if (elapsedTime - debrisComp.ValueRO.SpawnTime >= debrisComp.ValueRO.Lifetime)
                {
                    body.Destroy(); // LowLevelPhysics2D에서 해당 바디 해제
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // B. 인벤토리가 가득 찼는지 여부 판단 (자석 비활성화용)
                bool hasSpace = HasInventorySpace(ref inventory, inventorySize, slotSize, itemType);

                // C. 자석 인력 작용 조건: 픽업 범위 내에 있고, 가방에 빈 자리가 존재할 때
                if (dist <= pickupRange && hasSpace)
                {
                    float2 dir = math.normalize(diff);
                    // 플레이어 쪽으로 자연스럽게 빨려 들어가도록 물리 속도를 보간 및 설정
                    body.linearVelocity = math.lerp(body.linearVelocity, dir * 12.0f, dt * 15.0f);
                }

                // D. 획득 충돌 처리: 거리 0.4f 이내이며, 스폰된 지 0.15초 이상 지나 무작위 튕김이 진정된 시점
                if (dist <= 0.4f && (elapsedTime - debrisComp.ValueRO.SpawnTime >= 0.15f) && hasSpace)
                {
                    // 1. 인벤토리 버퍼에 누적 반영
                    AddToInventory(ref inventory, itemType, slotSize);
 
                    // 2. 물리 바디 파괴 (메모리 누수 원천 제거)
                    body.Destroy();
 
                    // 3. 엔티티 소멸
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        // 특정 아이템 타입이 인벤토리에 들어갈 슬롯 공간이 있는지 체크하는 헬퍼 함수
        [BurstCompile]
        private static bool HasInventorySpace(ref DynamicBuffer<InventoryBuffer> inventory, int inventorySize, int slotSize, int itemType)
        {
            // 1. 기존 슬롯 중 동일 아이템이고 공간 여유가 있는 슬롯이 있으면 획득 가능
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ItemType == itemType && inventory[i].Count < slotSize)
                {
                    return true;
                }
            }
            // 2. 여유 슬롯이 없다면, 빈 슬롯(ItemType == 0)이 존재해야 획득 가능
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ItemType == 0)
                {
                    return true;
                }
            }
            return false;
        }

        // 인벤토리 버퍼에 아이템을 집어넣는 헬퍼 함수
        [BurstCompile]
        private void AddToInventory(ref DynamicBuffer<InventoryBuffer> inventory, int itemType, int slotSize)
        {
            BlobAssetReference<ItemDatabaseBlob> dbRef = SystemAPI.GetSingleton<ItemDatabaseReference>().Reference;
            float maxStackMultiplier = 1.0f;

            ref var items = ref dbRef.Value.Items;
            for (int j = 0; j < items.Length; j++)
            {
                if (items[j].ItemID == itemType)
                {
                    maxStackMultiplier = items[j].MaxStackMultiplier;
                    break;
                }
            }
            int slotRealSize = (int)math.round(slotSize * maxStackMultiplier);

            // 1. 기존 슬롯 중 동일 아이템이고 공간 여유가 있는 슬롯에 추가
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ItemType == itemType)
                {
                    if (inventory[i].Count < slotRealSize)
                    {
                        var elem = inventory[i];
                        elem.Count += 1;
                        inventory[i] = elem;
                        return;
                    }
                }
            }
            // 2. 비어 있는 첫 번째 슬롯(ItemType == 0)에 등록
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ItemType == 0)
                {
                    var elem = inventory[i];
                    elem.ItemType = itemType;
                    elem.Count = 1;
                    inventory[i] = elem;
                    return;
                }
            }
        }
    }
}
