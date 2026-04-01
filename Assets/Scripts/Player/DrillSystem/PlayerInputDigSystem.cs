using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.LowLevelPhysics2D;
using CoreDriller.Player.StatSystem;

namespace CoreDriller.Player.DrillSystem
{
    // 마우스 클릭 시 DigEvent를 생성하는 시스템 (메인 스레드 실행)
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PlayerInputDigSystem : ISystem
    {
        public PhysicsQuery.QueryFilter drillQueryFilter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>(); // PlayerTag가 존재할 때만 업데이트되도록 설정

            var mask = PhysicsMask.All;
            mask.ResetBit(3);
            drillQueryFilter = new PhysicsQuery.QueryFilter(PhysicsMask.One, mask);

        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 1. 필요한 데이터가 없으면 즉시 종료 (Early Return)
            if (!SystemAPI.TryGetSingleton<MouseInputData>(out var mouseInput) || !mouseInput.IsLeftClickPressed) return;
            if (!SystemAPI.TryGetSingleton<PlayerDrillData>(out var playerDrillData)) return;

            // 2. 플레이어 위치 가져오기 (SingletonEntity 활용)
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
            float2 playerPosition = playerTransform.Position.xy; // .xy 스위즐링 활용

            float2 direction = mouseInput.WorldPosition - playerPosition;
            float distance = math.length(direction);

            // 3. 레이캐스트 설정
            float2 normalizedDir = distance > 1e-5f ? math.normalize(direction) : new float2(1, 0);
            float raycastDist = math.min(distance, playerDrillData.DrillRange);

            var rayInput = new PhysicsQuery.CastRayInput(playerPosition, normalizedDir * raycastDist);

            // 4. 레이캐스트 실행
            var hits = PhysicsWorld.defaultWorld.CastRay(rayInput, drillQueryFilter);
            float2 hitPosition = (hits.Length > 0) ? hits[0].point : playerPosition + (normalizedDir * raycastDist);
            hits.Dispose();

            // 5. ECB를 사용하여 엔티티 생성 (성능 최적화)
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(state.WorldUnmanaged);

            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new DigEvent
            {
                WorldPosition = hitPosition,
                Radius = playerDrillData.DrillExplosionRadius,
                DigPower = playerDrillData.DrillPower
            });
        }
    }
}