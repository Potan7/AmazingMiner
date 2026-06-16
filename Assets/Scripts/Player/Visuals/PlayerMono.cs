using CoreDriller.Player;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class PlayerMono : MonoBehaviour
{
    private Entity PlayerEntity => PlayerManager.Instance.PlayerEntity;

    void Update()
    {
        if (PlayerEntity == Entity.Null) return;

        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var playerTransform = entityManager.GetComponentData<LocalTransform>(PlayerEntity);

        transform.position = new Vector3(playerTransform.Position.x, playerTransform.Position.y, transform.position.z);
    }
}
