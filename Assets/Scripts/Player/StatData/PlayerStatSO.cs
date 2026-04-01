
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStatSO", menuName = "ScriptableObjects/PlayerStatSO", order = 0)]
public class PlayerStatSO : ScriptableObject
{
    // 게임 시작 시 설정되는 최초의 플레이어 스탯 데이터. 이후 게임 진행 중에는 PlayerStat 클래스의 인스턴스가 이 데이터를 기반으로 생성되어 사용됨.

    [Header("Base Status")]
    [Tooltip("플레이어의 최대 체력")]
    public float MaxHealth = 100f;

    [Tooltip("드릴링 및 제트팩 공유 자원 (최대 연료 탱크 용량)")]
    public float MaxFuel = 100f;

    [Tooltip("지상/지하 기본 이동 속도")]
    public float MoveSpeed = 3f;

    [Header("Mining (Drill) System")]
    [Tooltip("드릴 파워")]
    public float DrillPower = 1.0f;

    [Tooltip("채굴 속도 배율")]
    public float DrillSpeed = 1.0f;

    [Tooltip("1회 채굴 혹은 초당 채굴 시 소모되는 연료량")]
    public float DrillFuelConsumption = 1.0f;

    [Tooltip("채굴 조작의 재사용 대기시간")]
    public float DigCooldown = 0.5f;

    [Tooltip("곡괭이/드릴이 닿는 작업 반경")]
    public float DrillRange = 1.0f;

    [Tooltip("드릴의 폭발반경")]
    public float DrillExplosionRadius = 1.0f;

    [Header("Jetpack System")]
    [Tooltip("비행을 위한 제트팩 추진력")]
    public float JetpackThrust = 5f;

    [Tooltip("초당 비행 시 소모되는 연료량")]
    public float JetpackFuelConsumption = 1f;

    [Header("Logistics & Penalty")]
    [Tooltip("가방 최대 용량 (한 번에 끌어올릴 수 있는 원석 수량)")]
    public int InventorySize = 5;

    [Tooltip("떨어진 광물/아이템을 빨아들이는 반경")]
    public float ItemPickupRange = 2f;

    [Range(0f, 1f)]
    [Tooltip("강제 귀환 시 고가치 아이템 유실 경감 비율 (0 = 전부 패널티 대상, 1 = 100% 보호)")]
    public float PenaltyReductionRate = 0f;
}