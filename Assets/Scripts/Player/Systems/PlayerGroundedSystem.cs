using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.LowLevelPhysics2D;

namespace CoreDriller.Player.Systems
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct PlayerGroundedSystem : ISystem
    {
        private PhysicsQuery.QueryFilter groundedFilter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();

            // 지형(비트 0, 카테고리 1)과의 충돌만 판정
            var contacts = PhysicsMask.One; // 비트 0 = Terrain/Ground
            var categories = PhysicsMask.All;
            categories.ResetBit(3); // 플레이어 본인(비트 3) 무시
            categories.ResetBit(4); // 파편/아이템(비트 4) 무시

            groundedFilter = new PhysicsQuery.QueryFilter(contacts, categories);
        }

        public void OnUpdate(ref SystemState state)
        {
            PhysicsWorld world = PhysicsWorld.defaultWorld;
            if (!world.isValid) return;

            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            if (!SystemAPI.HasComponent<LocalTransform>(playerEntity) || !SystemAPI.HasComponent<PlayerGroundedData>(playerEntity)) return;

            var localTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
            var groundedData = SystemAPI.GetComponentRW<PlayerGroundedData>(playerEntity);

            float2 start = localTransform.Position.xy;
            //float rayLength = math.max(groundedData.ValueRO.RayLength, 1.4f); // 캡슐 콜라이더 높이를 감안하여 최소 1.4f 보장
            //float2 vector = new float2(0f, -rayLength);
            float2 vector = new float2(0f, -groundedData.ValueRO.RayLength);

            var rayInput = new PhysicsQuery.CastRayInput(start, vector);
            var hits = world.CastRay(rayInput, groundedFilter);

            groundedData.ValueRW.IsGrounded = hits.Length > 0;

            hits.Dispose();
        }
    }
}
