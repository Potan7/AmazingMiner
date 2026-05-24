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

            // 광물 생성 규칙 설정 (나중에는 외부 데이터에서 가져오게 됨)
            // Frequency를 낮춰 덩어리를 크게 만들고, Threshold를 높여 희귀도를 올렸습니다.
            var mineralRules = new NativeArray<MineralRule>(5, Allocator.Temp);
            mineralRules[0] = new MineralRule { BlockType = BlockTypes.Coal, MinDepth = 0.05f, MaxDepth = 0.5f, Frequency = 0.08f, Threshold = 0.75f, Hardness = 1.0f, MiningTime = 1.5f, MaxHP = 1.0f };
            mineralRules[1] = new MineralRule { BlockType = BlockTypes.Iron, MinDepth = 0.15f, MaxDepth = 0.7f, Frequency = 0.07f, Threshold = 0.82f, Hardness = 2.0f, MiningTime = 2.5f, MaxHP = 1.0f };
            mineralRules[2] = new MineralRule { BlockType = BlockTypes.Copper, MinDepth = 0.3f, MaxDepth = 0.9f, Frequency = 0.06f, Threshold = 0.85f, Hardness = 2.0f, MiningTime = 2.5f, MaxHP = 1.0f };
            mineralRules[3] = new MineralRule { BlockType = BlockTypes.Gold, MinDepth = 0.6f, MaxDepth = 1.0f, Frequency = 0.05f, Threshold = 0.92f, Hardness = 5.0f, MiningTime = 4.0f, MaxHP = 1.0f };
            mineralRules[4] = new MineralRule { BlockType = BlockTypes.Abyssite, MinDepth = 0.85f, MaxDepth = 1.0f, Frequency = 0.04f, Threshold = 0.95f, Hardness = 10.0f, MiningTime = 8.0f, MaxHP = 1.0f };

            // 1. GenerateStageRequest를 가진 엔티티를 찾습니다.
            foreach (var (request, entity) in SystemAPI.Query<RefRO<GenerateStageRequest>>().WithEntityAccess())
            {
                UnityEngine.Debug.Log($"[GenerateTerrainSystem] 지형 생성 시작! 시드: {request.ValueRO.Seed}");

                // 2. 로직 실행
                Generate(ref ecb, request.ValueRO, mineralRules);

                // 3. 처리 끝났으므로 요청 엔티티 파괴
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            mineralRules.Dispose();
        }


        private void Generate(ref EntityCommandBuffer ecb, in GenerateStageRequest request, NativeArray<MineralRule> mineralRules)
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

                    GenerateChunkTerrain(ref blockBuffer, chunkX, chunkY, chunkWidth, chunkDepth, blockHardness, request.Seed, mineralRules);
                }
            }
        }

        // 수직으로 깊은 맵 (테두리는 파괴 불가 벽, 내부는 흙과 광물 혼합)
        private void GenerateChunkTerrain(ref DynamicBuffer<BlockBuffer> blockBuffer, int chunkX, int chunkY, int chunkWidth, int chunkDepth, float blockHardness, uint seed, NativeArray<MineralRule> mineralRules)
        {
            float totalMaxDepthBlocks = chunkDepth * ChunkSize;

            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    BlockData blockData = new BlockData();

                    // 테두리 판별
                    bool isLeftWall = (chunkX == 0 && x == 0);
                    bool isRightWall = (chunkX == chunkWidth - 1 && x == ChunkSize - 1);
                    bool isBottomWall = (chunkY == chunkDepth - 1 && y == 0);

                    if (isLeftWall || isRightWall || isBottomWall)
                    {
                        blockData.BlockType = BlockTypes.Bedrock;
                        blockData.Hardness = float.MaxValue;
                        blockData.MiningTime = float.MaxValue;
                        blockData.MaxHP = float.MaxValue;
                        blockData.CurrentHP = float.MaxValue;
                    }
                    else
                    {
                        // 1. 기본 흙 설정 (T1: 경도 0.5 * 난이도 보정, 시간 1.0, HP 1.0)
                        blockData.BlockType = BlockTypes.Dirt;
                        blockData.Hardness = 0.5f * blockHardness;
                        blockData.MiningTime = 1.0f;
                        blockData.MaxHP = 1.0f;
                        blockData.CurrentHP = 1.0f;

                        // 2. 광물 배치 로직 (심도 비율 계산)
                        float currentDepthBlocks = (chunkY * ChunkSize) + y;
                        float depthRatio = currentDepthBlocks / totalMaxDepthBlocks;

                        // 월드 좌표 기반 노이즈 시드 생성
                        float2 worldPos = new float2((chunkX * ChunkSize) + x, currentDepthBlocks);

                        // 각 광물 규칙 검사 (가장 희귀한 광물부터 덮어씀)
                        for (int i = 0; i < mineralRules.Length; i++)
                        {
                            var rule = mineralRules[i];

                            // 심도 범위 체크
                            if (depthRatio < rule.MinDepth || depthRatio > rule.MaxDepth) continue;

                            // Simplex Noise
                            // 좌표에 주파수를 곱하고 시드를 오프셋으로 사용
                            float noiseVal = noise.snoise(worldPos * rule.Frequency + (float)seed * 0.123f);

                            // 0~1 범위로 정규화 (snoise는 -1~1 반환)
                            float normalizedNoise = (noiseVal + 1f) * 0.5f;

                            if (normalizedNoise > rule.Threshold)
                            {
                                blockData.BlockType = rule.BlockType;
                                blockData.Hardness = rule.Hardness;
                                blockData.MiningTime = rule.MiningTime;
                                blockData.MaxHP = rule.MaxHP;
                                blockData.CurrentHP = rule.MaxHP;
                                // 더 희귀한 광물을 아래에 배치하려면 i가 큰 쪽을 나중에 덮어씌움
                            }
                        }
                    }

                    blockBuffer.Add(new BlockBuffer { Value = blockData });
                }
            }
        }
    }
}