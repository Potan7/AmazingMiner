using System;
using Unity.Entities;

namespace CoreDriller.Player.StatSystem
{
    public static class PlayerUIEvents
    {
        // 1. 연료 이벤트 (현재, 최대)
        public static event Action<float, float> OnFuelChanged;
        public static float CurrentFuel { get; private set; }
        public static float MaxFuel { get; private set; }

        public static void TriggerFuelChanged(float current, float max)
        {
            CurrentFuel = current;
            MaxFuel = max;
            OnFuelChanged?.Invoke(current, max);
        }

        // 2. 체력 이벤트 (현재, 최대)
        public static event Action<float, float> OnHealthChanged;
        public static float CurrentHealth { get; private set; }
        public static float MaxHealth { get; private set; }

        public static void TriggerHealthChanged(float current, float max)
        {
            CurrentHealth = current;
            MaxHealth = max;
            OnHealthChanged?.Invoke(current, max);
        }

        // 3. 드릴 상태 이벤트 (쿨타임 타이머, 액티브 여부)
        public static event Action<float, bool> OnDrillStateChanged;
        public static float DrillTimer { get; private set; }
        public static bool IsDrillActive { get; private set; }

        public static void TriggerDrillStateChanged(float timer, bool isActive)
        {
            DrillTimer = timer;
            IsDrillActive = isActive;
            OnDrillStateChanged?.Invoke(timer, isActive);
        }

        // 4. 인벤토리 이벤트 (버퍼 변경 알림)
        public static event Action OnInventoryChanged;

        public static void TriggerInventoryChanged()
        {
            OnInventoryChanged?.Invoke();
        }
    }
}
