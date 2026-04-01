using Unity.Entities;
using Unity.Mathematics;

namespace CoreDriller
{
    // 1회성 채굴/폭발 명령을 담는 데이터
    public struct DigEvent : IComponentData
    {
        public float2 WorldPosition; // 터지는 중심점
        public float Radius;         // 파괴 반경 (드릴: 0.5f, 폭탄: 3.0f 등)
        public float DigPower;       // 파괴력 (블록의 Hardness와 비교)
    }

    public struct MouseInputData : IComponentData
    {
        public bool IsLeftClickPressed;
        public float2 WorldPosition;
    }
}