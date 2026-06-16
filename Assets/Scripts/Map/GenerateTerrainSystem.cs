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
        private const float BlockSize = 0.6f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GenerateStageRequest>();
            state.RequireForUpdate<BlockDatabaseReference>(); // 데이터베이스 로딩 완료 시점 대기
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var dbRef = SystemAPI.GetSingleton<BlockDatabaseReference>().Reference;

            // 광물 생성 규칙 설정 (나중에는 외부 데이터에서 가져오게 됨)
            // Frequency를 낮춰 덩어리를 크게 만들고, Threshold를 높여 희귀도를 올렸습니다.
            var mineralRules = new NativeArray<MineralRule>(4, Allocator.Temp);
            mineralRules[0] = new MineralRule { BlockType = BlockTypes.Coal, MinDepth = 0.05f, MaxDepth = 0.5f, Frequency = 0.08f, Threshold = 0.75f };
            mineralRules[1] = new MineralRule { BlockType = BlockTypes.Copper, MinDepth = 0.15f, MaxDepth = 0.7f, Frequency = 0.07f, Threshold = 0.76f }; // Copper is now shallower, threshold lowered to increase quantity
            mineralRules[2] = new MineralRule { BlockType = BlockTypes.Iron, MinDepth = 0.3f, MaxDepth = 0.9f, Frequency = 0.06f, Threshold = 0.82f };   // Iron is now deeper
            mineralRules[3] = new MineralRule { BlockType = BlockTypes.Gold, MinDepth = 0.6f, MaxDepth = 1.0f, Frequency = 0.05f, Threshold = 0.92f };

            // 1. GenerateStageRequest를 가진 엔티티를 찾습니다.
            foreach (var (request, entity) in SystemAPI.Query<RefRO<GenerateStageRequest>>().WithEntityAccess())
            {
                UnityEngine.Debug.Log($"[GenerateTerrainSystem] 지형 생성 시작! 시드: {request.ValueRO.Seed}");

                // 2. 로직 실행
                Generate(ref ecb, request.ValueRO, mineralRules, dbRef);

                // 3. 처리 끝났으므로 요청 엔티티 파괴
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            mineralRules.Dispose();
        }


        private void Generate(ref EntityCommandBuffer ecb, in GenerateStageRequest request, NativeArray<MineralRule> mineralRules, BlobAssetReference<BlockDatabaseBlob> dbRef)
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

                    GenerateChunkTerrain(ref blockBuffer, chunkX, chunkY, chunkWidth, chunkDepth, blockHardness, request.Seed, mineralRules, dbRef);
                }
            }
        }

        // 수직으로 깊은 맵 (테두리는 파괴 불가 벽, 내부는 흙과 광물 혼합)
        private void GenerateChunkTerrain(
            ref DynamicBuffer<BlockBuffer> blockBuffer, 
            int chunkX, 
            int chunkY, 
            int chunkWidth, 
            int chunkDepth, 
            float blockHardness, 
            uint seed, 
            NativeArray<MineralRule> mineralRules,
            BlobAssetReference<BlockDatabaseBlob> dbRef)
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
                        blockData = GetBlockDataFromDB(BlockTypes.Bedrock, 1.0f, dbRef);
                    }
                    else
                    {
                        // 심도 비율 계산 (y축의 뒤집힌 방향성을 바로잡아 아래로 갈수록 깊어지도록 계산)
                        float currentDepthBlocks = (chunkY * ChunkSize) + (ChunkSize - 1 - y);
                        float depthRatio = currentDepthBlocks / totalMaxDepthBlocks;

                        // 1/3 지점부터 돌(Abyssstone 대용)을 기본 블록으로 설정하고, 그 이전은 기본 흙으로 설정
                        if (depthRatio < 1.0f / 3.0f)
                        {
                            blockData = GetBlockDataFromDB(BlockTypes.Dirt, blockHardness, dbRef);
                        }
                        else
                        {
                            blockData = GetBlockDataFromDB(BlockTypes.Stone, blockHardness, dbRef);
                        }

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
                                blockData = GetBlockDataFromDB(rule.BlockType, 1.0f, dbRef);
                            }
                        }
                    }

                    blockBuffer.Add(new BlockBuffer { Value = blockData });
                }
            }
        }

        // 블록 데이터베이스에서 블록 정보를 안전하게 조회하여 BlockData 생성
        private static BlockData GetBlockDataFromDB(int blockType, float hardnessMultiplier, BlobAssetReference<BlockDatabaseBlob> dbRef)
        {
            BlockData blockData = new BlockData
            {
                BlockType = blockType,
                Hardness = 1.0f,
                MiningTime = 1.0f,
                MaxHP = 1.0f,
                CurrentHP = 1.0f,
                HasDecal = false
            };

            if (blockType == BlockTypes.Empty)
            {
                blockData.Hardness = 0f;
                blockData.MiningTime = 0f;
                return blockData;
            }

            ref var blocks = ref dbRef.Value.Blocks;
            for (int i = 0; i < blocks.Length; i++)
            {
                if (blocks[i].BlockType == blockType)
                {
                    var blockInfo = blocks[i];
                    // Bedrock은 절대 부서질 수 없도록 무한 설정 유지
                    if (blockType == BlockTypes.Bedrock)
                    {
                        blockData.Hardness = float.MaxValue;
                        blockData.MiningTime = float.MaxValue;
                        blockData.MaxHP = float.MaxValue;
                        blockData.CurrentHP = float.MaxValue;
                    }
                    else
                    {
                        blockData.Hardness = blockInfo.Hardness * hardnessMultiplier;
                        blockData.MiningTime = blockInfo.MiningTime;
                        blockData.MaxHP = blockInfo.MaxHP;
                        blockData.CurrentHP = blockInfo.MaxHP;
                    }
                    break;
                }
            }

            return blockData;
        }
    }
}