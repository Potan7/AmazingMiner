using UnityEngine;

namespace HomeScene
{
    public class InteractPoint : MonoBehaviour
    {
        public enum InteractType
        {
            None,
            Shop, Factory, Storage, Mine, Quest
        }

        public InteractType interactType = InteractType.None;

        public GameObject informationText;

        private HomeInteractManager _homeInteractManager;

        public void Init(HomeInteractManager homeInteractManager)
        {
            _homeInteractManager = homeInteractManager;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (interactType == InteractType.None || !collision.CompareTag("Player")) return;
            
            if (CoreDriller.UI.HomeUIManager.Instance != null)
            {
                CoreDriller.UI.HomeUIManager.Instance.ShowPrompt(GetPromptMessage());
            }
            else if (informationText != null)
            {
                informationText.SetActive(true);
            }

            _homeInteractManager.currentInteractPoint = this;
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (interactType == InteractType.None || !collision.CompareTag("Player")) return;
            
            if (CoreDriller.UI.HomeUIManager.Instance != null)
            {
                CoreDriller.UI.HomeUIManager.Instance.HidePrompt();
            }
            else if (informationText != null)
            {
                informationText.SetActive(false);
            }

            if (_homeInteractManager.currentInteractPoint == this)
            {
                _homeInteractManager.currentInteractPoint = null;
            }
        }

        private string GetPromptMessage()
        {
            switch (interactType)
            {
                case InteractType.Shop: return "[E] 상점 열기";
                case InteractType.Storage: return "[E] 창고 열기";
                case InteractType.Quest: return "[E] 의뢰 게시판 확인";
                case InteractType.Mine: return "[E] 광산 진입";
                case InteractType.Factory: return "[E] 공장 관제 모드 시작";
                default: return "[E] 상호작용";
            }
        }


        
        // 플레이어가 가까이 가면 이름과 상호작용 키 띄우기
        // 누르면 타입에 따라 행동 발생
        // 상점 - 상점 패널 열기
        // 공장 - 공장 카메라 전환
        // 창고 - 창고 패널 열기
        // 광산 - 메인 씬 진입
        // 퀘스트 - 퀘스트 패널 열기
    }
}
