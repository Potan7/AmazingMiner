using Unity.Entities;
using Unity.Mathematics;

namespace CoreDriller.Motion.Movement
{
    public struct MovementInput : IComponentData
    {
        public float2 Direction;
        public bool Jump;
    }

    public struct MovementStats : IComponentData
    {
        public float MoveSpeed;
        public float JumpForce;
    }
}
