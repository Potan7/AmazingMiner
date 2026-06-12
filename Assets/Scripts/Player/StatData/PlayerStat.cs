
using UnityEngine;

[System.Serializable]
public class PlayerStat
{
    // 게임 진행 중에 실제로 사용되는 플레이어 스탯 데이터 클래스. PlayerStatSO의 데이터를 기반으로 인스턴스가 생성되어 게임 내에서 사용됨.

    [Header("Base Status")]
    public float MaxHealth;
    public float MaxFuel;
    public float MoveSpeed;

    [Header("Mining (Drill) System")]
    public float DrillPower;
    public float DrillSpeed;
    public float DrillFuelConsumption;
    public float DigCooldown;
    public float DrillRange;
    public float DrillExplosionRadius;

    [Header("Jetpack System")]
    public float JetpackThrust;
    public float JetpackFuelConsumption;

    [Header("Logistics & Penalty")]
    public int InventorySlotSize; // 각 슬롯에 들어갈 수 있는 아이템 최대치
    public int InventorySlotCount;  // 슬롯의 총 개수
    public float ItemPickupRange;
    public float PenaltyReductionRate;

    public PlayerStat(PlayerStatSO statSO)
    {
        MaxHealth = statSO.MaxHealth;
        MaxFuel = statSO.MaxFuel;
        MoveSpeed = statSO.MoveSpeed;
        DrillPower = statSO.DrillPower;
        DrillSpeed = statSO.DrillSpeed;
        DrillFuelConsumption = statSO.DrillFuelConsumption;
        DigCooldown = statSO.DigCooldown;
        DrillRange = statSO.DrillRange;
        DrillExplosionRadius = statSO.DrillExplosionRadius;
        JetpackThrust = statSO.JetpackThrust;
        JetpackFuelConsumption = statSO.JetpackFuelConsumption;
        InventorySlotSize = statSO.InventorySlotSize;
        InventorySlotCount = statSO.InventorySlotCount;
        ItemPickupRange = statSO.ItemPickupRange;
        PenaltyReductionRate = statSO.PenaltyReductionRate;
    }
}