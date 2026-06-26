using CoreDriller.UI;
using Cysharp.Threading.Tasks;
using Potan.CoreUtils;
using UnityEngine;
using R3;

namespace HomeScene
{
    public class HomeInteractManager : MonoBehaviour
    {
        // 상호작용 키를 받아 상호작용을 대신 수행
        // 상호작용의 종류에 따라 작업 수행
        public InteractPoint[] interactPoints;
        
        public InteractPoint currentInteractPoint;
        

        private void Start()
        {
            PlayerManager.Instance.OnInteractKeyPerformed.Subscribe(OnInteractKey)
                .AddTo(destroyCancellationToken);

            foreach (var point in interactPoints)
            {
                point.Init(this);
            }
        }

        private void OnInteractKey(Unit _)
        {
            DevLog.Log($"Player Interact Key: {currentInteractPoint}");
            if (currentInteractPoint == null) return;

            switch (currentInteractPoint.interactType)
            {
                case InteractPoint.InteractType.Shop:
                    HomeUIManager.Instance.ShowPanel(InteractPoint.InteractType.Shop);
                    break;
                case InteractPoint.InteractType.Factory:
                    HomeUIManager.Instance.EnterFactoryMode();
                    break;
                case InteractPoint.InteractType.Storage:
                    HomeUIManager.Instance.ShowPanel(InteractPoint.InteractType.Storage);
                    break;
                case InteractPoint.InteractType.Mine:
                    HomeUIManager.Instance.ShowPanel(InteractPoint.InteractType.Mine);
                    break;
                case InteractPoint.InteractType.Quest:
                    HomeUIManager.Instance.ShowPanel(InteractPoint.InteractType.Quest);
                    break;
                case InteractPoint.InteractType.None:
                default:
                    DevLog.LogWarning("Interact Type Error");
                    break;
            }
        }
    }
}