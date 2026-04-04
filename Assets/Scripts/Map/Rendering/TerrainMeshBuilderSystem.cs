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

            // 텍스처 아틀라스 설정 (4x4 그리드)
            const float atlasSize = 4.0f;
            const float uvStep = 1.0f / atlasSize;

            // 청크 내 모든 블록을 순회하며 쿼드 생성
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    int blockType = blocks[x * ChunkSize + y].Value.BlockType;
                    // Empty(0)는 렌더링하지 않음
                    if (blockType == BlockTypes.Empty)
                        continue;

                    // UV 계산 (BlockType을 아틀라스 인덱스로 직접 사용)
                    // 0번 인덱스가 투명이므로, Dirt(1) -> 1번 인덱스, Bedrock(2) -> 2번 인덱스...
                    int uvIdx = blockType; 

                    int xIdx = uvIdx % (int)atlasSize;
                    int yIdx = uvIdx / (int)atlasSize;

                    float uvX = xIdx * uvStep;
                    // 좌측 상단이 0번이므로 Y축을 반전시킵니다.
                    float uvY = 1.0f - ((yIdx + 1) * uvStep); 

                    float2 uvMin = new float2(uvX, uvY);
                    float2 uvMax = new float2(uvX + uvStep, uvY + uvStep);

                    float px = x * CellSize;
                    float py = y * CellSize;

                    // 4개의 정점 (0.5f 사이즈 반영)
                    float3 vBL = new float3(px, py, 0);
                    float3 vBR = new float3(px + CellSize, py, 0);
                    float3 vTR = new float3(px + CellSize, py + CellSize, 0);
                    float3 vTL = new float3(px, py + CellSize, 0);

                    // UV 좌표 (Bl, Br, Tr, Tl)
                    float2 uvBL = new float2(uvMin.x, uvMin.y);
                    float2 uvBR = new float2(uvMax.x, uvMin.y);
                    float2 uvTR = new float2(uvMax.x, uvMax.y);
                    float2 uvTL = new float2(uvMin.x, uvMax.y);

                    AddQuad(vBL, vBR, vTR, vTL, uvBL, uvBR, uvTR, uvTL, ref vertices, ref triangles);
                }
            }
        }

        private void AddQuad(float3 a, float3 b, float3 c, float3 d,
                             float2 uva, float2 uvb, float2 uvc, float2 uvd,
                             ref DynamicBuffer<ChunkVertex> vertices, ref DynamicBuffer<ChunkTriangle> triangles)
        {
            int startIndex = vertices.Length;

            vertices.Add(new ChunkVertex { Position = a, UV = uva });
            vertices.Add(new ChunkVertex { Position = b, UV = uvb });
            vertices.Add(new ChunkVertex { Position = c, UV = uvc });
            vertices.Add(new ChunkVertex { Position = d, UV = uvd });

            triangles.Add(new ChunkTriangle { Value = startIndex });
            triangles.Add(new ChunkTriangle { Value = startIndex + 2 });
            triangles.Add(new ChunkTriangle { Value = startIndex + 1 });

            triangles.Add(new ChunkTriangle { Value = startIndex });
            triangles.Add(new ChunkTriangle { Value = startIndex + 3 });
            triangles.Add(new ChunkTriangle { Value = startIndex + 2 });
        }
    }
}
