using CoreDriller.Motion.Movement;
using Unity.Entities;
using UnityEngine;

namespace CoreDriller.Player
{
    class PlayerAuthoring : MonoBehaviour
    {
        [Header("Grounded Detection")]
        public float RayLength = 1.4f;

        class PlayerAuthoringBaker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PlayerTag());

                AddComponent(entity, new MovementInput());
                AddComponent(entity, new MovementStats()
                {
                    MoveSpeed = 5f,
                    JumpForce = 10f
                });

                AddComponent(entity, new PlayerGroundedData()
                {
                    IsGrounded = false,
                    RayLength = authoring.RayLength
                });
            }
        }
    }

    public partial struct PlayerTag : IComponentData
    {
        // simple tag component to identify the player entity
    }

    public struct PlayerGroundedData : IComponentData
    {
        public bool IsGrounded;
        public float RayLength;
    }

}
