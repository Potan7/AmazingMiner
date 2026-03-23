using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.LowLevelPhysics2D;

namespace CoreDriller.Map
{
    public struct ChunkRect
    {
        public float CenterX;
        public float CenterY;
        public float Width;
        public float Height;
    }

    [BurstCompile]
    partial struct GreedyMeshingJob : IJobEntity
    {
        [ReadOnly] public NativeArray<BlockBuffer> Blocks;
        public int ChunkSize;
        public float CellSize;
        public NativeList<ChunkRect> OutputRects;

        public void Execute()
        {
            var visited = new NativeArray<bool>(ChunkSize * ChunkSize, Allocator.Temp);

            for (int startX = 0; startX < ChunkSize; startX++)
            {
                for (int startY = 0; startY < ChunkSize; startY++)
                {
                    int startIndex = startX * ChunkSize + startY;

                    if (visited[startIndex] || Blocks[startIndex].Value.BlockType == 0)
                        continue;

                    int h = 0;
                    for (int y = startY; y < ChunkSize; y++)
                    {
                        int idx = startX * ChunkSize + y;
                        if (visited[idx] || Blocks[idx].Value.BlockType == 0) break;
                        h++;
                    }

                    int w = 0;
                    bool canExpand = true;
                    for (int x = startX; x < ChunkSize && canExpand; x++)
                    {
                        for (int y = startY; y < startY + h; y++)
                        {
                            int idx = x * ChunkSize + y;
                            if (visited[idx] || Blocks[idx].Value.BlockType == 0)
                            {
                                canExpand = false;
                                break;
                            }
                        }
                        if (canExpand) w++;
                    }

                    for (int x = startX; x < startX + w; x++)
                    {
                        for (int y = startY; y < startY + h; y++)
                        {
                            visited[x * ChunkSize + y] = true;
                        }
                    }

                    float centerX = (startX + w * 0.5f) * CellSize;
                    float centerY = (startY + h * 0.5f) * CellSize;

                    OutputRects.Add(new ChunkRect
                    {
                        CenterX = centerX,
                        CenterY = centerY,
                        Width = w * CellSize,
                        Height = h * CellSize
                    });
                }
            }
            visited.Dispose();
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GenerateTerrainSystem))]
    public partial struct TerrainPhysicsSystem : ISystem
    {
        private const float CellSize = 0.5f; 
        private const int ChunkSize = 16;
        private const int MaxProcessedChunksPerFrame = 32; // 처리량 증가 (초기 생성 병목 해결)

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            int processedCount = 0;

            // 1. 물리 갱신이 필요한 청크(PhysicsNeedsUpdateTag)만 찾습니다.
            foreach (var (blocks, transform, entity) in SystemAPI.Query<DynamicBuffer<BlockBuffer>, RefRO<LocalTransform>>()
                         .WithAll<PhysicsNeedsUpdateTag>()
                         .WithEntityAccess())
            {
                if (processedCount >= MaxProcessedChunksPerFrame) break;
                if (blocks.Length != ChunkSize * ChunkSize) continue;

                processedCount++;
                ecb.RemoveComponent<PhysicsNeedsUpdateTag>(entity); 

                // 🚨 기존에 이미 물리 바디가 붙어있다면 파괴 (누수 방지)
                if (SystemAPI.HasComponent<ChunkPhysicsBody>(entity))
                {
                    var existingBody = SystemAPI.GetComponent<ChunkPhysicsBody>(entity).Body;
                    if (existingBody.isValid)
                    {
                        existingBody.Destroy();
                    }
                    ecb.RemoveComponent<ChunkPhysicsBody>(entity);
                }

                // 2. Greedy Meshing 알고리즘 실행
                var rects = new NativeList<ChunkRect>(Allocator.Temp);
                var job = new GreedyMeshingJob
                {
                    Blocks = blocks.AsNativeArray(),
                    ChunkSize = ChunkSize,
                    CellSize = CellSize,
                    OutputRects = rects
                };
                job.Execute(); 

                // 3. LowLevelPhysics2D 바디 생성
                PhysicsWorld world = PhysicsWorld.defaultWorld;
                PhysicsBodyDefinition bodyDef = PhysicsBodyDefinition.defaultDefinition;
                bodyDef.type = PhysicsBody.BodyType.Static;
                bodyDef.position = transform.ValueRO.Position.xy;

                PhysicsBody body = world.CreateBody(bodyDef);

                if (rects.Length > 0)
                {
                    var polygonArray = new NativeArray<PolygonGeometry>(rects.Length, Allocator.Temp);

                    for (int i = 0; i < rects.Length; i++)
                    {
                        var rect = rects[i];
                        Vector2 boxSize = new Vector2(rect.Width, rect.Height);
                        PhysicsTransform boxOffset = new PhysicsTransform(new Vector2(rect.CenterX, rect.CenterY));

                        polygonArray[i] = PolygonGeometry.CreateBox(boxSize, 0f, boxOffset);
                    }

                    body.CreateShapeBatch(polygonArray.AsReadOnlySpan(), PhysicsShapeDefinition.defaultDefinition);
                    polygonArray.Dispose();
                }

                rects.Dispose();
                ecb.AddComponent(entity, new ChunkPhysicsBody { Body = body });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

}