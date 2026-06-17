// 각 블록의 데이터를 담는 컴포넌트
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.LowLevelPhysics2D;

namespace CoreDriller.Map
{

    public static class BlockTypes
    {
        public const int Empty = 0;
        public const int Bedrock = 1; // 2 -> 1
        public const int Dirt = 2;    // 1 -> 2
        public const int Stone = 3;   // 7 -> 3
        
        public const int Coal = 1001;   // 3 -> 1001
        public const int Iron = 1002;   // 4 -> 1002
        public const int Copper = 1003; // 5 -> 1003
        public const int Gold = 1004;   // 6 -> 1004
    }

    // Blob Asset에 들어갈 Unmanaged 블록 데이터 구조체
    public struct BlockBlobInfo
    {
        public int BlockType;
        public float Hardness;
        public float MiningTime;
        public float MaxHP;
        public BlobArray<float4> UVRects; // 여러 스프라이트 베리에이션 영역
        public int DropItemID;
    }

    // 전체 블록 데이터베이스 Blob 구조체
    public struct BlockDatabaseBlob
    {
        public BlobArray<BlockBlobInfo> Blocks;
    }

    // 블록 데이터베이스 참조 컴포넌트
    public struct BlockDatabaseReference : IComponentData
    {
        public BlobAssetReference<BlockDatabaseBlob> Reference;
    }

    public struct BlockData : IComponentData
    {
        public int BlockType;
        public float Hardness;    // 채굴 경도 (H)
        public float MiningTime;  // 기본 소요 시간 (T)
        public float MaxHP;       // 최대 내구도
        public float CurrentHP;   // 현재 남은 내구도
        public int VariantIndex;  // 랜덤 외형 스프라이트 인덱스
        public bool HasDecal;     // 이 블록에 균열 데칼이 생성되었는지 여부
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
        public float MiningTime;  // 기본 소요 시간 (T)
        public float MaxHP;       // 기본 최대 HP
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