using Unity.Entities;
using Unity.Burst;

namespace CoreDriller.Map.Rendering
{
    // 1. 이 시스템은 MeshNeedsUpdateTag가 달린 청크 엔티티를 찾아서 MarchingSquaresJob을 실행합니다.
    // 2. MarchingSquaresJob은 각 청크의 블록 데이터를 읽어서 정점과 삼각형 버퍼를 생성합니다.
    // 3. 이 시스템은 TerrainGpuUploadSystem보다 먼저 실행되어야 합니다. (그래야 GPU 업로드 전에 데이터가 준비됨)

    // 렌더링 업로드 시스템(TerrainGpuUploadSystem)보다 먼저 실행되도록 순서 강제
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(TerrainGpuUploadSystem))]
    public partial struct TerrainMeshBuilderSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // MeshNeedsUpdateTag가 달린 청크가 있을 때만 시스템이 활성화되도록 설정
            state.RequireForUpdate<MeshNeedsUpdateTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // MeshNeedsUpdateTag가 달린 청크(방금 생성됐거나, 방금 채굴당한 청크)만 처리합니다.
            var marchingJob = new BlockMeshJob
            {
                ChunkSize = 16,
                CellSize = 0.5f
            };

            // IJobEntity를 활용해 모든 태그된 청크에 대해 워커 스레드에서 병렬(Parallel) 연산을 수행합니다.
            state.Dependency = marchingJob.ScheduleParallel(state.Dependency);
        }
    }
}