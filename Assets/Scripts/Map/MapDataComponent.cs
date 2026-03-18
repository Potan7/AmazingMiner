// 각 블록의 데이터를 담는 컴포넌트
using Unity.Entities;
using Unity.Mathematics;

public struct BlockData : IComponentData
{
    public int BlockType; // 0: 빈 공간, 1: 흙, 2: 돌 등
    public float Hardness; // 블록의 경도
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