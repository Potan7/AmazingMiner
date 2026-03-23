using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using CoreDriller.Motion;
using UnityEngine;

namespace CoreDriller.Motion.Movement
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
            physicsBodyHandle.Body.linearVelocity = new Vector2(moveDelta.x, physicsBodyHandle.Body.linearVelocity.y);
            // physicsBodyHandle.Body.ApplyForceToCenter(moveDelta * 10f);                                                                                                 

            if (input.Jump)
            {
                // 점프 입력이 있을 때, 수직 방향으로 점프 힘을 가함. 일단 제트팩 로직으로 인해 이렇게 했는데 만약 잡몹도 점프를 한다면 수정이 필요
                physicsBodyHandle.Body.ApplyForceToCenter(new Vector2(0, stats.JumpForce));
            }
        }
    }
}

