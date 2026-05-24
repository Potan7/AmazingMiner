using UnityEngine;
using TMPro;
using Unity.Entities;
using CoreDriller.Player.StatSystem;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace CoreDriller.UI
{
    public class FuelUI : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI fuelText;

        private EntityManager entityManager;
        private EntityQuery fuelQuery;

        // 값 변경 감지를 위한 상태 캐싱
        private float lastCurrentFuel = -1f;
        private float lastMaxFuel = -1f;

        async void Start()
        {
            await UniTask.WaitWhile(() => World.DefaultGameObjectInjectionWorld == null); // 월드가 준비될 때까지 대기

            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            fuelQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerMovementData>());

            UpdateFuelLoop(destroyCancellationToken).Forget();
        }

        async UniTask UpdateFuelLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!fuelQuery.IsEmpty)
                {
                    var movementData = fuelQuery.GetSingleton<PlayerMovementData>();

                    // 4. 이전 프레임과 값이 다를 때만 UI 업데이트 (Dirty Flag 체크)
                    // (만약 값이 int형이라면 lastCurrentFuel != movementData.CurrentFuel 로 비교)
                    if (!Mathf.Approximately(lastCurrentFuel, movementData.CurrentFuel) ||
                        !Mathf.Approximately(lastMaxFuel, movementData.MaxFuel))
                    {
                        lastCurrentFuel = movementData.CurrentFuel;
                        lastMaxFuel = movementData.MaxFuel;

                        // SetText 자체 오버로드를 사용하여 가비지 발생 최소화
                        fuelText.SetText("{0} : {1}", movementData.CurrentFuel, movementData.MaxFuel);
                    }
                }
                await UniTask.Delay(100); // 0.1초마다 체크
            }
        }
    }
}