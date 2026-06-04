using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using CoreDriller.Map.Rendering;
using Unity.Burst;
using UnityEngine.LowLevelPhysics2D;

namespace CoreDriller.Map.Dig
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TerrainModificationSystem : ISystem
    {
        private const int ChunkSize = 16;
        private const float BlockSize = 0.6f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // 시스템 시작 시 IBufferElementData를 담을 글로벌 이벤트 엔티티(싱글톤) 생성
            var eventEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddBuffer<DigEvent>(eventEntity);

            state.RequireForUpdate<DigEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 1. 이벤트 버퍼 및 맵 설정(싱글톤) 확보
            if (!SystemAPI.HasSingleton<MapConfigData>())
            {
                return; // MapConfigData가 베이킹 완료될 때까지 대기
            }

            var config = SystemAPI.GetSingleton<MapConfigData>();
            var bufferEntity = SystemAPI.GetSingletonEntity<DigEvent>();
            var digBuffer = SystemAPI.GetBuffer<DigEvent>(bufferEntity);

            if (digBuffer.IsEmpty)
            {
                return;
            }

            // 2. 병렬 Job을 통해 블록 데이터를 수정합니다.
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var modJob = new TerrainModificationJob
            {
                DigEvents = digBuffer.AsNativeArray(), 
                ChunkSize = ChunkSize,
                BlockSize = BlockSize,
                ECB = ecb.AsParallelWriter(),
                SpritePrefab = config.SpritePrefab,
                DebrisPrefab = config.DebrisPrefab,
                ElapsedTime = SystemAPI.Time.ElapsedTime
            };

            state.Dependency = modJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete(); // Job 완료 대기

            // 3. 구조적 변화(Playback)가 일어나기 전에 이벤트를 먼저 클리어합니다.
            // Playback 이후에는 digBuffer가 무효화(Invalidated)되어 접근 시 예외가 발생합니다.
            digBuffer.Clear();

            // 4. 수정된 청크들에 대해 물리 갱신 태그 추가 (Structural Change 발생)
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    public partial struct TerrainModificationJob : IJobEntity
    {
        [ReadOnly] public NativeArray<DigEvent> DigEvents;
        public int ChunkSize;
        public float BlockSize;
        public EntityCommandBuffer.ParallelWriter ECB;
        public Entity SpritePrefab;
        public Entity DebrisPrefab;
        public double ElapsedTime;

        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in ChunkComponent chunk, ref DynamicBuffer<BlockBuffer> blocks)
        {
            float chunkWorldSize = ChunkSize * BlockSize;
            int2 coord = chunk.Coordinate;
            float chunkStartX = coord.x * chunkWorldSize;
            float chunkStartY = -coord.y * chunkWorldSize;

            bool chunkModified = false;

            for (int e = 0; e < DigEvents.Length; e++)
            {
                var ev = DigEvents[e];
                float2 centerPos = ev.WorldPosition;
                float radiusSq = ev.Radius * ev.Radius;

                // 청크 범위 체크 (최적화)
                // 현재 좌표계: chunkStartY에서 시작하여 위로(Positive Y) 블록이 쌓임
                float2 chunkMin = new float2(chunkStartX, chunkStartY);
                float2 chunkMax = new float2(chunkStartX + chunkWorldSize, chunkStartY + chunkWorldSize);

                // AABB와 원의 충돌 검사 (대략적)
                if (centerPos.x < chunkMin.x - ev.Radius || centerPos.x > chunkMax.x + ev.Radius ||
                    centerPos.y < chunkMin.y - ev.Radius || centerPos.y > chunkMax.y + ev.Radius)
                    continue;

                for (int i = 0; i < blocks.Length; i++)
                {
                    int x = i / ChunkSize;
                    int y = i % ChunkSize;

                    var blockData = blocks[i].Value;
                    
                    // 소프트 게이팅: 드릴 파워 P (ev.DigPower)가 블록 경도 H (blockData.Hardness)보다 작거나 같으면 채굴 속도가 0 이하가 됨
                    if (blockData.BlockType == 0 || ev.DigPower <= blockData.Hardness) continue;

                    float blockWorldX = chunkStartX + (x * BlockSize) + (BlockSize * 0.5f);
                    float blockWorldY = chunkStartY + (y * BlockSize) + (BlockSize * 0.5f);

                    float2 dist = centerPos - new float2(blockWorldX, blockWorldY);
                    if (math.lengthsq(dist) <= radiusSq)
                    {
                        // 기획서 2.1절 채굴 방정식 적용: R = ((P - H) * S) / T
                        float R = ((ev.DigPower - blockData.Hardness) * ev.DigSpeed) / blockData.MiningTime;
                        float damage = R * ev.DeltaTime;

                        blockData.CurrentHP -= damage;

                        // 데미지를 입었으나 완전히 깨지지는 않았고 아직 데칼이 안 붙은 경우 데칼 스폰
                        if (blockData.CurrentHP < blockData.MaxHP && blockData.CurrentHP > 0f && !blockData.HasDecal && SpritePrefab != Entity.Null)
                        {
                            blockData.HasDecal = true;

                            // 데칼 엔티티 생성
                            var decal = ECB.Instantiate(chunkIndex, SpritePrefab);

                            // 블록 월드 포지션 계산 (Z좌표는 블록보다 약간 앞인 -0.01f로 오버레이)
                            float3 decalPos = new float3(blockWorldX, blockWorldY, -0.01f);
                            ECB.SetComponent(chunkIndex, decal, Unity.Transforms.LocalTransform.FromPositionRotationScale(decalPos, quaternion.identity, BlockSize * 0.9f));

                            // 매핑 및 태그 등록
                            ECB.AddComponent(chunkIndex, decal, new DamageDecalTag 
                            { 
                                ChunkEntity = entity, 
                                BlockIndex = i 
                            });

                            // UV Rect 초기 상태 바인딩 (1x8 시트의 첫 번째 프레임: ScaleX=0.125, ScaleY=1.0, OffsetX=0.0, OffsetY=0.0)
                            ECB.AddComponent(chunkIndex, decal, new UVRect 
                            { 
                                Value = new float4(0.125f, 1f, 0f, 0f) 
                            });
                        }

                        if (blockData.CurrentHP <= 0f)
                        {
                            int originalType = blockData.BlockType;
                            blockData.BlockType = 0;
                            blockData.HasDecal = false; // 데칼이 없는 상태로 해제 (소멸 처리는 DamageDecalSystem에서 진행)
                            chunkModified = true; // 블록이 완전히 파괴되었을 때만 렌더링/물리 리빌드 트리거

                            // 흙(Dirt=1)이나 자원 광석들(Coal=3, Iron=4, Copper=5, Gold=6, Abyssite=7)에 대해서만 물리 파편을 드랍합니다. (Bedrock=2는 드랍 없음)
                            if (originalType != BlockTypes.Empty && originalType != BlockTypes.Bedrock && DebrisPrefab != Entity.Null)
                            {
                                // 1. 스프라이트 메쉬 엔티티 프리팹 인스턴스화
                                var debris = ECB.Instantiate(chunkIndex, DebrisPrefab);

                                // 2. 위치 및 크기 설정 (플레이어에게 잘 보이도록 Z축을 블록 앞인 -0.05f로 오버레이)
                                float3 debrisPos = new float3(blockWorldX, blockWorldY, -0.05f);
                                ECB.SetComponent(chunkIndex, debris, Unity.Transforms.LocalTransform.FromPositionRotationScale(debrisPos, quaternion.identity, BlockSize * 0.4f));

                                // 3. UVRect 연산 (추후 지형과 다른 별도의 아틀라스 스프라이트를 사용할 경우, 아래의 atlasSize 및 uvStep 계산 로직만 수정하시면 됩니다)
                                const float atlasSize = 4.0f; // 현재는 지형 grid.png의 4x4 아틀라스 격자를 사용
                                const float uvStep = 1.0f / atlasSize; // 0.25f
                                int uvIdx = originalType; 
                                int xIdx = uvIdx % (int)atlasSize;
                                int yIdx = uvIdx / (int)atlasSize;
                                float offsetX = xIdx * uvStep;
                                float offsetY = 1.0f - ((yIdx + 1) * uvStep);
                                ECB.AddComponent(chunkIndex, debris, new UVRect { Value = new float4(uvStep, uvStep, offsetX, offsetY) });

                                // 4. 파편 컴포넌트 데이터 등록
                                ECB.AddComponent<DebrisTag>(chunkIndex, debris);
                                ECB.AddComponent(chunkIndex, debris, new DebrisComponent
                                {
                                    ItemType = originalType,
                                    SpawnTime = (float)ElapsedTime,
                                    Lifetime = 30f // 30초 후 미획득 시 자동 소멸
                                });

                                // 5. 물리 컴포넌트 정보 등록 (동적 바디로 위쪽 사방으로 튀게 처리)
                                PhysicsBodyDefinition bodyDef = PhysicsBodyDefinition.defaultDefinition;
                                bodyDef.type = PhysicsBody.BodyType.Dynamic;
                                bodyDef.gravityScale = 1.0f; // 중력 가속도

                                // 시드 기반 난수를 통해 위쪽 퍼짐 속도 생성 (e와 블록 위치 해싱)
                                uint seed = (uint)(blockWorldX * 1000f + blockWorldY * 100000f + e * 100f + 1);
                                Unity.Mathematics.Random rand = new Unity.Mathematics.Random(seed);
                                float vx = rand.NextFloat(-1.5f, 1.5f);
                                float vy = rand.NextFloat(2.5f, 5.0f); // 주로 위로 튕김

                                PhysicsShapeDefinition shapeDef = PhysicsShapeDefinition.defaultDefinition;

                                // 파편 충돌 필터(Collision Filter) 설정:
                                // 카테고리 16(4번째 비트)과 충돌 마스크 17(0번째 비트=지형, 4번째 비트=파편) 설정
                                // 플레이어(카테고리 8, 3번째 비트) 및 드릴 레이와의 충돌을 차단하여 획득을 자연스럽게 만듭니다.
                                var debrisCategory = new PhysicsMask();
                                debrisCategory.SetBit(4);

                                var debrisContacts = new PhysicsMask();
                                debrisContacts.SetBit(0); // 지형과 충돌 가능
                                debrisContacts.SetBit(4); // 파편끼리 충돌 가능

                                shapeDef.contactFilter = new PhysicsShape.ContactFilter
                                {
                                    categories = debrisCategory,
                                    contacts = debrisContacts,
                                    groupIndex = 0
                                };

                                ECB.AddComponent(chunkIndex, debris, new CoreDriller.Motion.PhysicsBodyInformation
                                {
                                    BodyDefinition = bodyDef,
                                    ShapeDefinition = shapeDef,
                                    ColliderType = CoreDriller.Motion.ColliderShapeType.Circle,
                                    CircleGeometry = CircleGeometry.Create(0.12f), // 작은 원형 충돌체 정의 (0.12f 크기)
                                    InitialVelocity = new float2(vx, vy)
                                });
                            }
                        }

                        blocks[i] = new BlockBuffer { Value = blockData };
                    }
                }
            }

            if (chunkModified)
            {
                // 렌더링 및 물리 갱신 태그 추가
                ECB.AddComponent<MeshNeedsUpdateTag>(chunkIndex, entity);
                ECB.AddComponent<PhysicsNeedsUpdateTag>(chunkIndex, entity);
            }
        }
    }

}