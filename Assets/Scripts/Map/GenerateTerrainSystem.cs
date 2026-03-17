using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct GenerateStageRequest : IComponentData
{
    public uint Seed;        // 맵 생성 시드
    public int Width;        // 맵 가로 크기 (청크 개수)
    public int Depth;        // 맵 세로 크기 (깊이)
    public int Difficulty;   // 난이도 (블록 단단함 보정치)
}

[BurstCompile]
public partial struct GenerateTerrainSystem : ISystem
{
    private const int ChunkSize = 16;
    private const float NoiseScale = 0.1f;
    private const float TerrainHeightAmplitude = 10f;
    private const float TerrainBaseHeight = 10f;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // GenerateStageRequest 컴포넌트가 있을때만 시스템이 활성화되도록 설정
        state.RequireForUpdate<GenerateStageRequest>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 1. GenerateStageRequest를 가진 엔티티를 찾습니다. (없으면 이 시스템은 그냥 패스됨)
        foreach (var (request, entity) in SystemAPI.Query<RefRO<GenerateStageRequest>>().WithEntityAccess())
        {
            // 2. 로직 실행 (전달받은 Seed, Width 등을 바탕으로 맵 생성)
            Generate(ref ecb, request.ValueRO);

            // 3. 처리가 끝났으므로 '요청 엔티티'를 파괴합니다. (1회성 호출 효과)
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private void Generate(ref EntityCommandBuffer ecb, in GenerateStageRequest request)
    {
        int chunkWidth = math.max(0, request.Width);
        int chunkDepth = math.max(0, request.Depth);
        uint seed = request.Seed == 0 ? 1u : request.Seed;
        float blockHardness = math.max(1f, 1f + request.Difficulty * 0.25f);

        for (int chunkX = 0; chunkX < chunkWidth; chunkX++)
        {
            for (int chunkY = 0; chunkY < chunkDepth; chunkY++)
            {
                Entity chunkEntity = ecb.CreateEntity();

                ecb.AddComponent(chunkEntity, new ChunkComponent
                {
                    Coordinate = new int2(chunkX, chunkY)
                });

                DynamicBuffer<BlockBuffer> blockBuffer = ecb.AddBuffer<BlockBuffer>(chunkEntity);
                GenerateChunkTerrain(blockBuffer, chunkX, chunkY, seed, blockHardness);
            }
        }
    }

    // Perlin Noise를 이용하여 청크 지형 생성
    private void GenerateChunkTerrain(DynamicBuffer<BlockBuffer> blockBuffer, int chunkX, int chunkY, uint seed, float blockHardness)
    {
        float seedOffset = (seed & 1023u) * 0.017f;

        for (int x = 0; x < ChunkSize; x++) // 청크 가로 크기
        {
            // Perlin Noise를 이용한 높이 계산
            float sampleX = (chunkX * ChunkSize + x + seedOffset) * NoiseScale;
            float height = noise.snoise(new float2(sampleX, seedOffset)) * TerrainHeightAmplitude + TerrainBaseHeight;

            for (int y = 0; y < ChunkSize; y++) // 청크 세로 크기
            {
                BlockData blockData = new BlockData();
                int worldY = chunkY * ChunkSize + y;

                if (worldY < height) // 높이 아래는 블록으로 채움
                {
                    blockData.BlockType = 1; // 예: 흙
                    blockData.Hardness = blockHardness;
                }
                else // 높이 위는 빈 공간
                {
                    blockData.BlockType = 0; // 예: 빈 공간
                    blockData.Hardness = 0f;
                }

                blockBuffer.Add(new BlockBuffer { Value = blockData });
            }
        }
    }
}