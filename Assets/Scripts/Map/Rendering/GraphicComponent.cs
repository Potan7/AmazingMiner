using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace CoreDriller.Map.Rendering
{
    // 셰이더 그래프의 _UVRect 속성에 바인딩되어 GPU Hybrid Instancing으로 즉시 업로드됨
    [MaterialProperty("_UVRect")]
    public struct UVRect : IComponentData
    {
        // x: OffsetX, y: OffsetY, z: ScaleX, w: ScaleY
        public float4 Value;
    }
}
