
using Unity.Entities;
using UnityEngine;

namespace CoreDriller.Player.StatSystem
{
    // 플레이어의 드릴 상태를 저장하는 싱글톤 컴포넌트
    public struct PlayerDrillData : IComponentData
    {
        public float MaxCooldown;    // 최대 쿨타임 (설정값)
        public float CurrentTimer;   // 남은 시간 (상태값)
        public bool IsActive;        // 현재 드릴 사용 중 여부
        public Unity.Mathematics.float2 LastHitPosition; // 최신 굴착 레이캐스트 타격 지점

        public float DrillPower;
        public float DrillSpeed;
        public float DrillFuelConsumption;
        public float DigCooldown;
        public float DrillRange;
        public float DrillExplosionRadius;
    }

    public struct PlayerMovementData : IComponentData
    {
        public float MoveSpeed;      // 이동 속도 (설정값)
        public float MaxFuel;        // 최대 연료 (설정값)
        public float CurrentFuel;    // 남은 연료 (상태값)
        public bool IsActive;        // 현재 제트팩 사용 중 여부

        public float JetpackThrust;
        public float JetpackFuelConsumption;

    }

    public struct PlayerHealthData : IComponentData
    {
        public float MaxHealth;      // 최대 체력 (설정값)
        public float CurrentHealth;  // 남은 체력 (상태값)
    }

    public struct PlayerInventoryData : IComponentData
    {
        public int InventorySize;    // 인벤토리 크기 (설정값)
        public float ItemPickupRange; // 아이템 픽업 범위 (설정값)
    }

    [InternalBufferCapacity(8)]
    public struct InventoryBuffer : IBufferElementData
    {
        public int ItemType;
        public int Count;
    }

    // 연료 고갈 시 강제 귀환을 처리하기 위한 태그
    public struct ForcedReturnTag : IComponentData {}
}