using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using CoreDriller.Physics;
using UnityEngine;

namespace CoreDriller.Movement
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(SyncTransformToPhysicsSystem))]
    public partial struct MovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MovementInput>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // foreach (var (input, stats, entity) in SystemAPI.Query<RefRO<MovementInput>, RefRO<MovementStats>>().WithEntityAccess())
            // {
            //     float2 moveDelta = input.ValueRO.Direction * stats.ValueRO.MoveSpeed * deltaTime;

            //     if (SystemAPI.HasComponent<PhysicsBodyHandle>(entity))
            //     {
            //         // Update physics body directly (so SyncTransformToPhysicsSystem will pick it up)
            //         var bodyHandle = SystemAPI.GetComponentRW<PhysicsBodyHandle>(entity);
            //         bodyHandle.ValueRW.Body.position += new Vector2(moveDelta.x, moveDelta.y);
            //     }
            //     else if (SystemAPI.HasComponent<LocalTransform>(entity))
            //     {
            //         // Fallback for non-physics entities
            //         var transform = SystemAPI.GetComponentRW<LocalTransform>(entity);
            //         transform.ValueRW.Position += new float3(moveDelta.x, moveDelta.y, 0);
            //     }
            // }

            var movementJob = new MovementJob();
            state.Dependency = movementJob.ScheduleParallel(state.Dependency);
        }


    }

    partial struct MovementJob : IJobEntity
    {
        public void Execute(in PhysicsBodyHandle physicsBodyHandle, ref MovementInput input, in MovementStats stats)
        {
            float2 moveDelta = stats.MoveSpeed * input.Direction;
            physicsBodyHandle.Body.linearVelocity = new Vector2(moveDelta.x, physicsBodyHandle.Body.linearVelocity.y); // ?�평 ?�동?� ?�력???�라, ?�직 ?�동?� 기존 ?�도 ?��?
            // physicsBodyHandle.Body.ApplyForceToCenter(moveDelta * 10f); // ?�의 ?�기??조절???�요?????�습?�다.                                                                                                   

            if (input.Jump)
            {
                // 제트팩: 누르고 있는 내내 위쪽으로 힘을 더합니다 (Force)
                // 중력을 이겨내려면 기존 점프 속도보다 гораздо 큰 힘(예: 수백~수천)이 필요할 수 있습니다.
                physicsBodyHandle.Body.ApplyForceToCenter(new Vector2(0, stats.JumpForce));
            }
        }
    }
}

