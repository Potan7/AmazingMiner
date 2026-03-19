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
            }
        }
    }

    public partial struct PlayerTag : IComponentData
    {
        // 플레이어 식별 태그 (추후 플레이어 관련 컴포넌트 추가 가능)
    }

}