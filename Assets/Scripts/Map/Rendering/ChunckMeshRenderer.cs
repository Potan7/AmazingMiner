using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CoreDriller.Map.Rendering
{

    // 1. 업데이트 태그: 이 태그가 붙은 청크는 정점/삼각형 데이터를 재생성해야 함을 나타냅니다.
    public struct MeshNeedsUpdateTag : IComponentData { }

    // 2. 정점/삼각형 버퍼 컴포넌트: 각 청크 엔티티에 붙어서 GPU에 보낼 데이터를 담습니다.
    [InternalBufferCapacity(0)] 
    public struct ChunkVertex : IBufferElementData { public float3 Position; }
    [InternalBufferCapacity(0)] 
    public struct ChunkTriangle : IBufferElementData { public int Value; }

    // 3. 렌더링 정보 컴포넌트: GPU 버퍼 핸들과 그릴 개수, 영역 등을 담습니다.
    public struct ChunkMeshRenderer : IComponentData
    {
        // GPU에 업로드된 정점/삼각형 데이터의 핸들
        public GraphicsBufferHandle VertexBuffer;
        public GraphicsBufferHandle IndexBuffer;

        // 그릴 개수
        public int VertexCount;
        public int IndexCount;

        // 그릴 영역 (Culling용)
        public AABB Bounds;
    }
}