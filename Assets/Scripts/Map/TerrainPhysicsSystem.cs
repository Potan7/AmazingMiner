using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.LowLevelPhysics2D;

namespace CoreDriller.Map
{

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GenerateTerrainSystem))]
    public partial struct TerrainPhysicsSystem : ISystem
    {
        private const float CellSize = 1f;
        private const int ChunkSize = 16;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (blocks, transform, entity) in SystemAPI.Query<DynamicBuffer<BlockBuffer>, RefRO<LocalTransform>>()
                         .WithNone<ChunkPhysicsBody>()
                         .WithEntityAccess())
            {
                PhysicsWorld world = PhysicsWorld.defaultWorld;
                PhysicsBodyDefinition bodyDef = PhysicsBodyDefinition.defaultDefinition;
                bodyDef.type = PhysicsBody.BodyType.Static;
                bodyDef.position = transform.ValueRO.Position.xy;

                PhysicsBody body = world.CreateBody(bodyDef);

                // 1. PhysicsComposer 생성
                var composer = PhysicsComposer.Create();

                // Span으로 넘기기 위한 1x1 기본 박스 (중심이 0,0 이고 범위는 -0.5 ~ 0.5)
                var baseBoxArray = new NativeArray<PolygonGeometry>(1, Allocator.Temp);
                baseBoxArray[0] = new PolygonGeometry();
                ReadOnlySpan<PolygonGeometry> baseBoxSpan = baseBoxArray.AsReadOnlySpan();

                // 2. 블록 순회 및 Composer에 오프셋 레이어 추가
                for (int i = 0; i < blocks.Length; i++)
                {
                    if (blocks[i].Value.BlockType > 0)
                    {
                        int x = i / ChunkSize;
                        int y = i % ChunkSize;

                        // [핵심] 기본 박스가 중심(0,0) 기준이므로, 
                        // 블록의 좌하단을 렌더링 메쉬와 똑같이 (x,y)에 맞추려면 오프셋에 0.5f를 더해야 합니다.
                        float px = x * CellSize + (CellSize * 0.5f);
                        float py = y * CellSize + (CellSize * 0.5f);
                        PhysicsTransform offset = new PhysicsTransform(new Vector2(px, py));

                        // Operation.OR: 인접한 블록끼리 겹치는 내부 충돌선을 없애고 하나로 융합합니다.
                        composer.AddLayer(baseBoxSpan, offset, PhysicsComposer.Operation.OR);
                    }
                }

                // 3. 병합된 최종 다각형 지오메트리 굽기 (이 함수의 반환값은 NativeArray이므로 using 사용 가능)
                using (var combinedShapes = composer.CreatePolygonGeometry(new Vector2(0.5f, 0.5f), Allocator.Temp))
                {
                    if (combinedShapes.Length > 0)
                    {
                        // 합쳐진 덩어리를 단 한 번의 호출로 바디에 부착 (Batch 최적화)
                        body.CreateShapeBatch(combinedShapes.AsReadOnlySpan(), PhysicsShapeDefinition.defaultDefinition);
                    }
                }

                // 4. 수동 메모리 해제
                composer.Destroy();
                baseBoxArray.Dispose();

                ecb.AddComponent(entity, new ChunkPhysicsBody { Body = body });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}