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

            float3 playerPos = playerTransform.Position;
            float pickupRange = inventoryData.ItemPickupRange;
            int inventorySize = inventoryData.InventorySize;
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
                bool hasSpace = HasInventorySpace(ref inventory, inventorySize, itemType);

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
                    AddToInventory(ref inventory, itemType);
 
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
        private static bool HasInventorySpace(ref DynamicBuffer<InventoryBuffer> inventory, int inventorySize, int itemType)
        {
            // 1. 기존 가방 슬롯에 동일한 아이템이 이미 존재하면 합쳐지므로 공간 여유와 상관없이 수집 가능
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ItemType == itemType)
                {
                    return true;
                }
            }
            // 2. 새로운 광물 종류라면 현재 가방 크기가 최대 제한 슬롯 개수 미만이어야 획득 가능
            return inventory.Length < inventorySize;
        }

        // 인벤토리 버퍼에 아이템을 집어넣는 헬퍼 함수
        [BurstCompile]
        private static void AddToInventory(ref DynamicBuffer<InventoryBuffer> inventory, int itemType)
        {
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ItemType == itemType)
                {
                    var elem = inventory[i];
                    elem.Count += 1;
                    inventory[i] = elem;
                    return;
                }
            }
            // 기존 가방에 동일 품목이 없는 경우 신규 등록
            inventory.Add(new InventoryBuffer { ItemType = itemType, Count = 1 });
        }
    }
}
