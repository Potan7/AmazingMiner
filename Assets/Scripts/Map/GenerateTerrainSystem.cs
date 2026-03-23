using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CoreDriller.Map
{
    public struct GenerateStageRequest : IComponentData
    {
        public uint Seed;        // 맵 생성 시드
        public int Width;        // 맵 가로 크기 (청크 개수)
        public int Depth;        // 맵 세로 크기 (깊이)
        public int Difficulty;   // 난이도 (블록 단단함 보정치)
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [BurstCompile]
    public partial struct GenerateTerrainSystem : ISystem
    {
        private const int ChunkSize = 16;
        private const float BlockSize = 0.5f;

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

            // 1. GenerateStageRequest를 가진 엔티티를 찾습니다.
            foreach (var (request, entity) in SystemAPI.Query<RefRO<GenerateStageRequest>>().WithEntityAccess())
            {
                UnityEngine.Debug.Log($"[GenerateTerrainSystem] 지형 생성 시작! 시드: {request.ValueRO.Seed}");

                // 2. 로직 실행
                Generate(ref ecb, request.ValueRO);

                // 3. 처리 끝났으므로 요청 엔티티 파괴
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }


        private void Generate(ref EntityCommandBuffer ecb, in GenerateStageRequest request)
        {
            int chunkWidth = math.max(0, request.Width);
            int chunkDepth = math.max(0, request.Depth);
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

#if UNITY_EDITOR
                    ecb.SetName(chunkEntity, $"Chunk_{chunkX}_{chunkY}");
#endif

                    DynamicBuffer<BlockBuffer> blockBuffer = ecb.AddBuffer<BlockBuffer>(chunkEntity);

                    // 각 청크의 월드 크기는 (ChunkSize * BlockSize) = (16 * 0.5) = 8 유닛입니다.
                    float chunkWorldSize = ChunkSize * BlockSize;
                    ecb.AddComponent(chunkEntity, LocalTransform.FromPosition(new float3(chunkX * chunkWorldSize, -chunkY * chunkWorldSize, 0)));
                    ecb.AddBuffer<Map.Rendering.ChunkVertex>(chunkEntity);
                    ecb.AddBuffer<Map.Rendering.ChunkTriangle>(chunkEntity);
                    ecb.AddComponent<Map.Rendering.MeshNeedsUpdateTag>(chunkEntity);
                    ecb.AddComponent<PhysicsNeedsUpdateTag>(chunkEntity);

                    GenerateChunkTerrain(ref blockBuffer, chunkX, chunkY, chunkWidth, chunkDepth, blockHardness);
                }
            }
        }

        // 수직으로 깊은 맵 (테두리는 파괴 불가 벽, 내부는 꽉 찬 흙)
        private void GenerateChunkTerrain(ref DynamicBuffer<BlockBuffer> blockBuffer, int chunkX, int chunkY, int chunkWidth, int chunkDepth, float blockHardness)
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    BlockData blockData = new BlockData();

                    // 테두리 판별: 왼쪽 벽, 오른쪽 벽, 바닥
                    bool isLeftWall = (chunkX == 0 && x == 0);
                    bool isRightWall = (chunkX == chunkWidth - 1 && x == ChunkSize - 1);
                    bool isBottomWall = (chunkY == chunkDepth - 1 && y == 0);

                    // 맨 윗단(지표면)의 테두리를 막을 것인가? 
                    // 위는 보통 뚫어두지만, 지저로 떨어지게만 만들거라면 좌/우/하단만 막는 것이 일반적입니다.
                    if (isLeftWall || isRightWall || isBottomWall)
                    {
                        blockData.BlockType = 2; // 파괴 불가 벽 (베드락 등)
                        blockData.Hardness = float.MaxValue;
                    }
                    else
                    {
                        // 맵 내부는 모두 흙으로 꽉 채웁니다
                        blockData.BlockType = 1; // 흙
                        blockData.Hardness = blockHardness;
                    }

                    blockBuffer.Add(new BlockBuffer { Value = blockData });
                }
            }
        }
    }
}