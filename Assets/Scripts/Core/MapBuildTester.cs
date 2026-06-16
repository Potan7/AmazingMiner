using Unity.Entities;
using UnityEngine;
using CoreDriller.Map;

public class MapBuildTester : MonoBehaviour
{
    void Start()
    {
        // 맵 생성 요청을 ECS로 보냅니다.
        OnClickEnterStage();
    }

    public void OnClickEnterStage()
    {
        // 1. 현재 실행 중인 ECS 월드와 EntityManager를 가져옵니다.
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        // 2. 빈 엔티티를 생성합니다.
        var requestEntity = entityManager.CreateEntity();

        // 3. 엔티티에 맵 생성 요청 데이터(매개변수)를 달아줍니다.
        entityManager.AddComponentData(requestEntity, new GenerateStageRequest
        {
            Seed = (uint)UnityEngine.Random.Range(1, 10000),
            Width = 10,
            Depth = 50, // 깊이 50짜리 스테이지
            Difficulty = 1
        });

        Debug.Log("스테이지 생성 요청을 ECS로 보냈습니다!");
    }
}
