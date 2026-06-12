using UnityEngine;
using TMPro;
using CoreDriller.Player.StatSystem;
using UnityEngine.UI;

namespace CoreDriller.UI
{
    public class FuelUI : MonoBehaviour
    {
        public Image fuelBar;
        public TextMeshProUGUI fuelText;

        void Start()
        {
            // 캐시된 초기값으로 즉시 UI 갱신
            UpdateFuelText(PlayerUIEvents.CurrentFuel, PlayerUIEvents.MaxFuel);

            // 이벤트 구독
            PlayerUIEvents.OnFuelChanged += UpdateFuelText;
        }

        void OnDestroy()
        {
            // 메모리 누수 방지 구독 해제
            PlayerUIEvents.OnFuelChanged -= UpdateFuelText;
        }

        private void UpdateFuelText(float currentFuel, float maxFuel)
        {
            fuelBar.fillAmount = maxFuel > 0 ? currentFuel / maxFuel : 0f;
            fuelText.SetText("{0} / {1}", Mathf.CeilToInt(currentFuel), Mathf.CeilToInt(maxFuel));
        }
    }
}