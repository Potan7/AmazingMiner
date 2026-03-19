using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.LowLevelPhysics2D;

namespace CoreDriller.Physics
{

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    partial struct PhysicsHandleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsBodyInformation>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (bodyInfo, localTransform, entity) in SystemAPI.Query<RefRO<PhysicsBodyInformation>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                PhysicsWorld world = PhysicsWorld.defaultWorld;
                PhysicsBody body = world.CreateBody(bodyInfo.ValueRO.BodyDefinition);
                body.CreateShape(bodyInfo.ValueRO.CircleGeometry, bodyInfo.ValueRO.ShapeDefinition);

                body.position = localTransform.ValueRO.Position.xy;

                ecb.AddComponent(entity, new PhysicsBodyHandle() { Body = body });
                ecb.RemoveComponent<PhysicsBodyInformation>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}