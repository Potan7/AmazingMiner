using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using CoreDriller.Map.Rendering;
using Unity.Burst;

namespace CoreDriller.Map.Dig
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TerrainModificationSystem : ISystem
    {
        private const int ChunkSize = 16;
        private const float BlockSize = 0.5f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 1. 이번 프레임에 발생한 모든 채굴 이벤트를 수집합니다.
            var digEvents = new NativeList<DigEvent>(Allocator.TempJob);
            foreach (var digEvent in SystemAPI.Query<RefRO<DigEvent>>())
            {
                digEvents.Add(digEvent.ValueRO);
            }

            if (digEvents.Length == 0)
            {
                digEvents.Dispose();
                return;
            }

            // 2. 병렬 Job을 통해 블록 데이터를 수정합니다.
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var modJob = new TerrainModificationJob
            {
                DigEvents = digEvents.AsArray(),
                ChunkSize = ChunkSize,
                BlockSize = BlockSize,
                ECB = ecb.AsParallelWriter()
            };

            state.Dependency = modJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            // 3. 수정된 청크들에 대해 물리 갱신 태그 추가 (ECB를 통해 안전하게 전달)
            // 물리 바디 파괴는 TerrainPhysicsSystem에서 PhysicsNeedsUpdateTag를 확인하여 처리합니다.

            // 4. 이벤트 엔티티 삭제
            foreach (var (_, entity) in SystemAPI.Query<RefRO<DigEvent>>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            digEvents.Dispose();
        }
    }

    [BurstCompile]
    public partial struct TerrainModificationJob : IJobEntity
    {
        [ReadOnly] public NativeArray<DigEvent> DigEvents;
        public int ChunkSize;
        public float BlockSize;
        public EntityCommandBuffer.ParallelWriter ECB;

        private void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, RefRO<ChunkComponent> chunk, DynamicBuffer<BlockBuffer> blocks)
        {
            float chunkWorldSize = ChunkSize * BlockSize;
            int2 coord = chunk.ValueRO.Coordinate;
            float chunkStartX = coord.x * chunkWorldSize;
            float chunkStartY = -coord.y * chunkWorldSize;

            bool chunkModified = false;

            for (int e = 0; e < DigEvents.Length; e++)
            {
                var ev = DigEvents[e];
                float2 centerPos = ev.WorldPosition;
                float radiusSq = ev.Radius * ev.Radius;

                // 청크 범위 체크 (최적화)
                // 현재 좌표계: chunkStartY에서 시작하여 위로(Positive Y) 블록이 쌓임
                float2 chunkMin = new float2(chunkStartX, chunkStartY);
                float2 chunkMax = new float2(chunkStartX + chunkWorldSize, chunkStartY + chunkWorldSize);

                // AABB와 원의 충돌 검사 (대략적)
                if (centerPos.x < chunkMin.x - ev.Radius || centerPos.x > chunkMax.x + ev.Radius ||
                    centerPos.y < chunkMin.y - ev.Radius || centerPos.y > chunkMax.y + ev.Radius)
                    continue;

                for (int i = 0; i < blocks.Length; i++)
                {
                    int x = i / ChunkSize;
                    int y = i % ChunkSize;

                    var blockData = blocks[i].Value;
                    if (blockData.BlockType == 0 || blockData.Hardness > ev.DigPower) continue;

                    float blockWorldX = chunkStartX + (x * BlockSize) + (BlockSize * 0.5f);
                    float blockWorldY = chunkStartY + (y * BlockSize) + (BlockSize * 0.5f);

                    float2 dist = centerPos - new float2(blockWorldX, blockWorldY);
                    if (math.lengthsq(dist) <= radiusSq)
                    {
                        blockData.BlockType = 0;
                        blocks[i] = new BlockBuffer { Value = blockData };
                        chunkModified = true;
                    }
                }
            }

            if (chunkModified)
            {
                // 렌더링 및 물리 갱신 태그 추가
                ECB.AddComponent<MeshNeedsUpdateTag>(chunkIndex, entity);
                ECB.AddComponent<PhysicsNeedsUpdateTag>(chunkIndex, entity);
            }
        }
    }

}