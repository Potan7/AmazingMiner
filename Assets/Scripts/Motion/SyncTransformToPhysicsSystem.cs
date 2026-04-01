using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.LowLevelPhysics2D;

namespace CoreDriller.Motion
{

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    partial struct SyncTransformToPhysicsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsBodyHandle>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // foreach (var (bodyHandle, localTransform) in SystemAPI.Query<RefRO<PhysicsBodyHandle>, RefRW<LocalTransform>>())
            // {
            // localTransform.ValueRW.Position = new float3(bodyHandle.ValueRO.Body.position.x, bodyHandle.ValueRO.Body.position.y, localTransform.ValueRO.Position.z);
            // }
            var syncJob = new SyncTransformJob();
            state.Dependency = syncJob.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    partial struct SyncTransformJob : IJobEntity
    {
        [BurstCompile]
        public void Execute(ref LocalTransform localTransform, in PhysicsBodyHandle bodyHandle)
        {
            localTransform.Position = new float3(bodyHandle.Body.position.x, bodyHandle.Body.position.y, localTransform.Position.z);
        }
    }
}
