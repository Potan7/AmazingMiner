using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CoreDriller.Map.Rendering
{
    // 1. 이 Job은 각 청크의 블록 데이터를 읽어서 정점과 삼각형 버퍼를 생성합니다.
    // 2. 이 Job은 TerrainMeshBuilderSystem에서 IJobEntity로 실행됩니다.
    // 3. Marching Squares 알고리즘 대신 간단히 블록이 있으면 네모(Quad)를 만드는 방식으로 구현했습니다.

    [BurstCompile]
    public partial struct BlockMeshJob : IJobEntity
    {
        public int ChunkSize; // 16
        public float CellSize; // 1f

        [BurstCompile]
        private void Execute(in ChunkComponent chunk, in DynamicBuffer<BlockBuffer> blocks,
                             ref DynamicBuffer<ChunkVertex> vertices, ref DynamicBuffer<ChunkTriangle> triangles)
        {
            vertices.Clear();
            triangles.Clear();

            // 1. 패딩 없이 정확히 16x16만 돕니다.
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    // 현재 블록이 비어있으면(0) 그냥 넘어갑니다.
                    if (blocks[x * ChunkSize + y].Value.BlockType == 0)
                        continue;

                    // 2. 흙이 있다면 해당 위치에 네모(Quad)를 생성합니다.
                    float px = x * CellSize;
                    float py = y * CellSize;

                    // 네모의 4개 꼭짓점
                    float3 vBL = new float3(px, py, 0);                           // 왼쪽 아래
                    float3 vBR = new float3(px + CellSize, py, 0);                // 오른쪽 아래
                    float3 vTR = new float3(px + CellSize, py + CellSize, 0);     // 오른쪽 위
                    float3 vTL = new float3(px, py + CellSize, 0);                // 왼쪽 위

                    // 3. 버퍼에 네모 데이터 추가
                    AddQuad(vBL, vBR, vTR, vTL, ref vertices, ref triangles);
                }
            }
        }

        [BurstCompile]
        private void AddQuad(float3 a, float3 b, float3 c, float3 d,
                             ref DynamicBuffer<ChunkVertex> vertices, ref DynamicBuffer<ChunkTriangle> triangles)
        {
            int startIndex = vertices.Length;

            vertices.Add(new ChunkVertex { Position = a });
            vertices.Add(new ChunkVertex { Position = b });
            vertices.Add(new ChunkVertex { Position = c });
            vertices.Add(new ChunkVertex { Position = d });

            // 첫 번째 삼각형 (a, b, c)
            triangles.Add(new ChunkTriangle { Value = startIndex });
            triangles.Add(new ChunkTriangle { Value = startIndex + 1 });
            triangles.Add(new ChunkTriangle { Value = startIndex + 2 });

            // 두 번째 삼각형 (a, c, d)
            triangles.Add(new ChunkTriangle { Value = startIndex });
            triangles.Add(new ChunkTriangle { Value = startIndex + 2 });
            triangles.Add(new ChunkTriangle { Value = startIndex + 3 });
        }
    }
}