using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using CoreDriller.Map.Rendering;
using Unity.Burst;

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
                DecalPrefab = config.DamageDecalPrefab
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
        public Entity DecalPrefab;

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
                        if (blockData.CurrentHP < blockData.MaxHP && blockData.CurrentHP > 0f && !blockData.HasDecal && DecalPrefab != Entity.Null)
                        {
                            blockData.HasDecal = true;

                            // 데칼 엔티티 생성
                            var decal = ECB.Instantiate(chunkIndex, DecalPrefab);

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
                            blockData.BlockType = 0;
                            blockData.HasDecal = false; // 데칼이 없는 상태로 해제 (소멸 처리는 DamageDecalSystem에서 진행)
                            chunkModified = true; // 블록이 완전히 파괴되었을 때만 렌더링/물리 리빌드 트리거
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