// 각 블록의 데이터를 담는 컴포넌트
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.LowLevelPhysics2D;

namespace CoreDriller.Map
{

    public static class BlockTypes
    {
        public const int Empty = 0;
        public const int Dirt = 1;
        public const int Bedrock = 2;
        public const int Coal = 3;
        public const int Iron = 4;
        public const int Copper = 5;
        public const int Gold = 6;
        public const int Abyssite = 7; // T5 심연석
    }

    public struct BlockData : IComponentData
    {
        public int BlockType;
        public float Hardness;
    }

    // 광물 생성 규칙을 정의하는 구조체
    public struct MineralRule
    {
        public int BlockType;
        public float MinDepth;    // 나타나기 시작하는 최소 심도 (0.0 ~ 1.0)
        public float MaxDepth;    // 사라지는 최대 심도 (0.0 ~ 1.0)
        public float Frequency;   // 노이즈 주파수 (덩어리 크기 결정)
        public float Threshold;   // 생성 문턱값 (높을수록 희귀함)
        public float Hardness;    // 광물의 단단함
    }

    // 하나의 청크를 관리하는 엔티티에 붙일 컴포넌트
    public struct ChunkComponent : IComponentData
    {
        public int2 Coordinate; // 청크의 그리드 좌표
    }

    // 청크 내의 블록 데이터를 관리하기 위한 Buffer 사용
    // 엔티티 당 블록 개수가 많으므로 DynamicBuffer가 적합합니다.
    [InternalBufferCapacity(16 * 16)] // 예: 16x16 청크
    public struct BlockBuffer : IBufferElementData
    {
        public BlockData Value;
    }

    // 청크의 물리 바디를 관리하는 컴포넌트
    public partial struct ChunkPhysicsBody : IComponentData
    {
        public PhysicsBody Body;
    }

    // 물리 갱신이 필요함을 나타내는 태그
    public struct PhysicsNeedsUpdateTag : IComponentData { }
}