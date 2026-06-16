using CoreDriller.Motion.Movement;
using Unity.Entities;
using UnityEngine;

namespace CoreDriller.Player
{
    class PlayerAuthoring : MonoBehaviour
    {

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
            }
        }
    }

    public partial struct PlayerTag : IComponentData
    {
        // simple tag component to identify the player entity
    }

}
