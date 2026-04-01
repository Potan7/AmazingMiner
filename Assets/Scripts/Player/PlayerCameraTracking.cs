using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace CoreDriller.Player
{

    public class PlayerCameraTracking : MonoBehaviour
    {
        private Entity playerEntity;

        void LateUpdate()
        {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (playerEntity == Entity.Null)
            {
                var playerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>());
                if (playerQuery.IsEmpty) return;

                playerEntity = playerQuery.GetSingletonEntity();
            }

            if (!entityManager.Exists(playerEntity)) return;

            // 플레이어의 위치를 카메라의 위치로 설정
            if (!entityManager.HasComponent<LocalTransform>(playerEntity)) return;
            var playerTransform = entityManager.GetComponentData<LocalTransform>(playerEntity);

            transform.position = new Vector3(playerTransform.Position.x, playerTransform.Position.y, transform.position.z);

        }
    }
}