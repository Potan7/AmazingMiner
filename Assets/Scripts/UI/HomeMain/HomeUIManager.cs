using UnityEngine;
using UnityEngine.UIElements;
using Potan.CoreUtils;
using HomeScene;
using System;

namespace CoreDriller.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class HomeUIManager : MonoSingleton<HomeUIManager>
    {
        [Header("Sub Panel UXML Templates")]
        public VisualTreeAsset shopPanelUxml;
        public VisualTreeAsset storagePanelUxml;
        public VisualTreeAsset questPanelUxml;
        public VisualTreeAsset minePanelUxml;
        public VisualTreeAsset factoryPanelUxml;

        private UIDocument _uiDocument;
        private VisualElement _root;
        
        // 메인 컨테이너 캐싱
        private VisualElement _hudContainer;
        private VisualElement _promptContainer;
        private VisualElement _modalContainer;
        private VisualElement _factoryContainer;

        // HUD 요소
        private Label _goldLabel;
        private Label _questSummaryLabel;

        // 프롬프트 요소
        private Label _promptLabel;

        // 동적 인스턴스화된 서브 패널 캐싱
        private VisualElement _shopPanel;
        private VisualElement _storagePanel;
        private VisualElement _questPanel;
        private VisualElement _minePanel;
        private VisualElement _factoryPanel;

        private const string HiddenClass = "hidden";

        protected override void OnAwake()
        {
            _uiDocument = GetComponent<UIDocument>();
            _root = _uiDocument.rootVisualElement;

            // 메인 컨테이너 쿼리
            _hudContainer = _root.Q<VisualElement>("hud-container");
            _promptContainer = _root.Q<VisualElement>("prompt-container");
            _modalContainer = _root.Q<VisualElement>("modal-container");
            _factoryContainer = _root.Q<VisualElement>("factory-container");

            // HUD / 프롬프트 요소 쿼리
            _goldLabel = _root.Q<Label>("gold-label");
            _questSummaryLabel = _root.Q<Label>("quest-summary-label");
            _promptLabel = _root.Q<Label>("prompt-label");

            InitializePanels();
        }

        private void InitializePanels()
        {
            // 각 패널을 CloneTree하여 해당하는 컨테이너 하위에 배치하고 숨김 처리
            if (shopPanelUxml != null && _modalContainer != null)
            {
                _shopPanel = shopPanelUxml.CloneTree();
                _shopPanel.AddToClassList(HiddenClass);
                _modalContainer.Add(_shopPanel);

                // 닫기 버튼 이벤트 바인딩
                var closeBtn = _shopPanel.Q<Button>("btn-close");
                if (closeBtn != null) closeBtn.clicked += HideAllPanels;
            }

            if (storagePanelUxml != null && _modalContainer != null)
            {
                _storagePanel = storagePanelUxml.CloneTree();
                _storagePanel.AddToClassList(HiddenClass);
                _modalContainer.Add(_storagePanel);

                var closeBtn = _storagePanel.Q<Button>("btn-close");
                if (closeBtn != null) closeBtn.clicked += HideAllPanels;
            }

            if (questPanelUxml != null && _modalContainer != null)
            {
                _questPanel = questPanelUxml.CloneTree();
                _questPanel.AddToClassList(HiddenClass);
                _modalContainer.Add(_questPanel);

                var closeBtn = _questPanel.Q<Button>("btn-close");
                if (closeBtn != null) closeBtn.clicked += HideAllPanels;
            }

            if (minePanelUxml != null && _modalContainer != null)
            {
                _minePanel = minePanelUxml.CloneTree();
                _minePanel.AddToClassList(HiddenClass);
                _modalContainer.Add(_minePanel);

                var closeBtn = _minePanel.Q<Button>("btn-close");
                if (closeBtn != null) closeBtn.clicked += HideAllPanels;

                var enterBtn = _minePanel.Q<Button>("btn-enter-mine");
                if (enterBtn != null)
                {
                    enterBtn.clicked += () => 
                    {
                        Debug.Log("지하 광산 씬 진입 버튼 클릭됨!");
                        // TODO: 실제 씬 로딩 로직 연동
                    };
                }
            }

            if (factoryPanelUxml != null && _factoryContainer != null)
            {
                _factoryPanel = factoryPanelUxml.CloneTree();
                _factoryPanel.AddToClassList(HiddenClass);
                _factoryContainer.Add(_factoryPanel);

                var exitBtn = _factoryPanel.Q<Button>("btn-exit-factory");
                if (exitBtn != null) exitBtn.clicked += ExitFactoryMode;
            }
        }

        // --- API ---

        /// <summary>특정 패널을 모달로 표시합니다.</summary>
        public void ShowPanel(InteractPoint.InteractType type)
        {
            HideAllPanels(); // 기존 열려 있는 거 정리

            VisualElement targetPanel = null;
            switch (type)
            {
                case InteractPoint.InteractType.Shop: targetPanel = _shopPanel; break;
                case InteractPoint.InteractType.Storage: targetPanel = _storagePanel; break;
                case InteractPoint.InteractType.Quest: targetPanel = _questPanel; break;
                case InteractPoint.InteractType.Mine: targetPanel = _minePanel; break;
            }

            if (targetPanel != null)
            {
                _modalContainer.RemoveFromClassList(HiddenClass);
                targetPanel.RemoveFromClassList(HiddenClass);

                // 플레이어 입력 차단
                if (PlayerManager.Instance != null)
                {
                    PlayerManager.Instance.TogglePlayerInput(false);
                }
            }
        }

        /// <summary>모든 모달 팝업을 닫고 플레이어 통제권을 복구합니다.</summary>
        public void HideAllPanels()
        {
            if (_modalContainer != null) _modalContainer.AddToClassList(HiddenClass);
            if (_shopPanel != null) _shopPanel.AddToClassList(HiddenClass);
            if (_storagePanel != null) _storagePanel.AddToClassList(HiddenClass);
            if (_questPanel != null) _questPanel.AddToClassList(HiddenClass);
            if (_minePanel != null) _minePanel.AddToClassList(HiddenClass);

            // 플레이어 조작 복구
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.TogglePlayerInput(true);
            }
        }

        /// <summary>상호작용 안내 프롬프트 표시</summary>
        public void ShowPrompt(string message)
        {
            if (_promptContainer == null || _promptLabel == null) return;
            
            _promptLabel.text = message;
            _promptContainer.RemoveFromClassList(HiddenClass);
        }

        /// <summary>상호작용 안내 프롬프트 숨김</summary>
        public void HidePrompt()
        {
            if (_promptContainer == null) return;
            _promptContainer.AddToClassList(HiddenClass);
        }

        /// <summary>공장 관제 모드 진입 (HUD 숨김 및 공장 전용 UI 활성화)</summary>
        public void EnterFactoryMode()
        {
            if (_hudContainer != null) _hudContainer.AddToClassList(HiddenClass);
            if (_factoryContainer != null) _factoryContainer.RemoveFromClassList(HiddenClass);
            if (_factoryPanel != null) _factoryPanel.RemoveFromClassList(HiddenClass);

            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.TogglePlayerInput(false);
            }
        }

        /// <summary>공장 관제 모드 종료 (HUD 복구 및 공장 전용 UI 비활성화)</summary>
        public void ExitFactoryMode()
        {
            if (_hudContainer != null) _hudContainer.RemoveFromClassList(HiddenClass);
            if (_factoryContainer != null) _factoryContainer.AddToClassList(HiddenClass);
            if (_factoryPanel != null) _factoryPanel.AddToClassList(HiddenClass);

            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.TogglePlayerInput(true);
            }
        }

        /// <summary>골드 UI 수동 갱신</summary>
        public void UpdateGold(int amount)
        {
            if (_goldLabel != null)
            {
                _goldLabel.text = $"{amount} G";
            }
        }
    }
}
