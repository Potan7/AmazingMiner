using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CoreDriller.Map.Rendering
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Dig.TerrainModificationSystem))]
    [BurstCompile]
    public partial struct DamageDecalSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            
            // ChunkEntity의 BlockBuffer를 조회하기 위한 ReadOnly Lookup 획득
            var blockBufferLookup = SystemAPI.GetBufferLookup<BlockBuffer>(true);

            var job = new DamageDecalUpdateJob
            {
                BlockBufferLookup = blockBufferLookup,
                ECB = ecb.AsParallelWriter()
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
            state.Dependency.Complete(); // 구조 변경(데칼 파괴)을 즉각 수행하기 위해 대기

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    public partial struct DamageDecalUpdateJob : IJobEntity
    {
        [ReadOnly] public BufferLookup<BlockBuffer> BlockBufferLookup;
        public EntityCommandBuffer.ParallelWriter ECB;

        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, ref UVRect decalUv, in DamageDecalTag decalTag)
        {
            // 1. 타겟 청크 엔티티의 유효성 및 블록 버퍼 소유 여부 확인
            if (!BlockBufferLookup.HasBuffer(decalTag.ChunkEntity))
            {
                ECB.DestroyEntity(chunkIndex, entity);
                return;
            }

            var blocks = BlockBufferLookup[decalTag.ChunkEntity];

            // 2. 인덱스 초과 방지 예외 처리
            if (decalTag.BlockIndex < 0 || decalTag.BlockIndex >= blocks.Length)
            {
                ECB.DestroyEntity(chunkIndex, entity);
                return;
            }

            var blockData = blocks[decalTag.BlockIndex].Value;

            // 3. 블록이 완전히 깨졌거나 빈 블록으로 처리된 경우 데칼 엔티티 소멸
            if (blockData.BlockType == BlockTypes.Empty || blockData.CurrentHP <= 0f)
            {
                ECB.DestroyEntity(chunkIndex, entity);
                return;
            }

            // 4. 1x8 스프라이트 시트 크랙 단계 계산 (0.0 ~ 1.0)
            // Y는 항상 1로 고정 (단일 행), X는 데미지 단계에 따라 0~7까지 컬럼 이동
            float damageRatio = blockData.CurrentHP / blockData.MaxHP;
            const int gridCount = 8;
            const float cellSize = 1f / gridCount; // 0.125f

            // HP 비율에 맞춰 0(균열 없음) ~ 7(최대 균열) 단계 매핑 
            int col = (int)math.clamp((1f - damageRatio) * gridCount, 0, gridCount - 1);

            // X축 시작 오프셋 계산
            float offsetX = col * cellSize;

            // 셰이더 그래프의 _UVRect 속성 매핑 구조:
            // Value.x = Scale X (cellSize = 0.125)
            // Value.y = Scale Y (1.0f)
            // Value.z = Offset X (offsetX)
            // Value.w = Offset Y (0.0f)
            decalUv.Value = new float4(cellSize, 1f, offsetX, 0f);
        }
    }

    // 균열 데칼 엔티티가 타겟 청크의 어느 인덱스 블록을 가리키는지 추적
    public struct DamageDecalTag : IComponentData
    {
        public Entity ChunkEntity;
        public int BlockIndex;
    }

}
