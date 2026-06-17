using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.LowLevelPhysics2D;

namespace CoreDriller.Motion
{

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct PhysicsHandleSystem : ISystem
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

                switch (bodyInfo.ValueRO.ColliderType)
                {
                    case ColliderShapeType.Circle:
                        body.CreateShape(bodyInfo.ValueRO.CircleGeometry, bodyInfo.ValueRO.ShapeDefinition);
                        break;
                    case ColliderShapeType.Capsule:
                        body.CreateShape(bodyInfo.ValueRO.CapsuleGeometry, bodyInfo.ValueRO.ShapeDefinition);
                        break;
                    case ColliderShapeType.Box:
                        body.CreateShape(bodyInfo.ValueRO.BoxGeometry, bodyInfo.ValueRO.ShapeDefinition);
                        break;
                }
                body.position = localTransform.ValueRO.Position.xy;

                if (bodyInfo.ValueRO.BodyDefinition.type == PhysicsBody.BodyType.Dynamic && 
                    math.lengthsq(bodyInfo.ValueRO.InitialVelocity) > 0f)
                {
                    body.linearVelocity = bodyInfo.ValueRO.InitialVelocity;
                }

                ecb.AddComponent(entity, new PhysicsBodyHandle() { Body = body });
                ecb.RemoveComponent<PhysicsBodyInformation>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
