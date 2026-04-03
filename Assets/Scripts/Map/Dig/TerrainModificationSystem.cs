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
        public void OnCreate(ref SystemState state)
        {
            // 시스템 시작 시 IBufferElementData를 담을 글로벌 이벤트 엔티티(싱글톤) 생성
            var eventEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddBuffer<DigEvent>(eventEntity);

            state.RequireForUpdate<DigEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 1. 이벤트 버퍼를 보관한 싱글톤 엔티티 확보
            var bufferEntity = SystemAPI.GetSingletonEntity<DigEvent>();
            var digBuffer = SystemAPI.GetBuffer<DigEvent>(bufferEntity);

            if (digBuffer.IsEmpty)
            {
                return;
            }

            // 2. 병렬 Job을 통해 블록 데이터를 수정합니다.
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var modJob = new TerrainModificationJob
            {
                DigEvents = digBuffer.AsNativeArray(), 
                ChunkSize = ChunkSize,
                BlockSize = BlockSize,
                ECB = ecb.AsParallelWriter()
            };

            state.Dependency = modJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete(); // Job 완료 대기

            // 3. 구조적 변화(Playback)가 일어나기 전에 이벤트를 먼저 클리어합니다.
            // Playback 이후에는 digBuffer가 무효화(Invalidated)되어 접근 시 예외가 발생합니다.
            digBuffer.Clear();

            // 4. 수정된 청크들에 대해 물리 갱신 태그 추가 (Structural Change 발생)
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    public partial struct TerrainModificationJob : IJobEntity
    {
        [ReadOnly] public NativeArray<DigEvent> DigEvents;
        public int ChunkSize;
        public float BlockSize;
        public EntityCommandBuffer.ParallelWriter ECB;

        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in ChunkComponent chunk, ref DynamicBuffer<BlockBuffer> blocks)
        {
            float chunkWorldSize = ChunkSize * BlockSize;
            int2 coord = chunk.Coordinate;
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