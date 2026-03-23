using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace CoreDriller.Map.Rendering
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(TerrainGpuUploadSystem))]
    public partial struct TerrainMeshBuilderSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MeshNeedsUpdateTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 수정된 청크만 처리하는 Job을 예약합니다.
            var marchingJob = new BlockMeshJob
            {
                ChunkSize = 16,
                CellSize = 0.5f
            };

            state.Dependency = marchingJob.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(MeshNeedsUpdateTag))] // 중요: 수정된 청크만 연산하도록 필터링
    public partial struct BlockMeshJob : IJobEntity
    {
        public int ChunkSize;
        public float CellSize;

        public void Execute(in ChunkComponent chunk, in DynamicBuffer<BlockBuffer> blocks,
                             ref DynamicBuffer<ChunkVertex> vertices, ref DynamicBuffer<ChunkTriangle> triangles)
        {
            vertices.Clear();
            triangles.Clear();

            // 청크 내 모든 블록을 순회하며 쿼드 생성
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    if (blocks[x * ChunkSize + y].Value.BlockType == 0)
                        continue;

                    float px = x * CellSize;
                    float py = y * CellSize;

                    // 4개의 정점 (0.5f 사이즈 반영)
                    float3 vBL = new float3(px, py, 0);
                    float3 vBR = new float3(px + CellSize, py, 0);
                    float3 vTR = new float3(px + CellSize, py + CellSize, 0);
                    float3 vTL = new float3(px, py + CellSize, 0);

                    AddQuad(vBL, vBR, vTR, vTL, ref vertices, ref triangles);
                }
            }
        }

        private void AddQuad(float3 a, float3 b, float3 c, float3 d,
                             ref DynamicBuffer<ChunkVertex> vertices, ref DynamicBuffer<ChunkTriangle> triangles)
        {
            int startIndex = vertices.Length;

            vertices.Add(new ChunkVertex { Position = a });
            vertices.Add(new ChunkVertex { Position = b });
            vertices.Add(new ChunkVertex { Position = c });
            vertices.Add(new ChunkVertex { Position = d });

            triangles.Add(new ChunkTriangle { Value = startIndex });
            triangles.Add(new ChunkTriangle { Value = startIndex + 2 });
            triangles.Add(new ChunkTriangle { Value = startIndex + 1 });

            triangles.Add(new ChunkTriangle { Value = startIndex });
            triangles.Add(new ChunkTriangle { Value = startIndex + 3 });
            triangles.Add(new ChunkTriangle { Value = startIndex + 2 });
        }
    }
}
