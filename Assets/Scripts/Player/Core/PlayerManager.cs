// System 제거 — Action 이벤트를 R3 Subject<Unit>으로 대체
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using CoreDriller.Player.StatSystem;
using CoreDriller.Player;
using Potan.CoreUtils;
using Cysharp.Threading.Tasks;
using R3;

[DefaultExecutionOrder(-100)] // 다른 시스템보다 먼저 실행되도록 설정 (필요에 따라 조정 가능)
public class PlayerManager : MonoSingleton<PlayerManager>
{

    public PlayerStatSO playerStatSO; // 에디터에서 설정할 수 있는 ScriptableObject 참조
    public PlayerStat CurrentStats { get; private set; } // 게임 내에서 실제로 사용되는 플레이어 스탯 인스턴스

    public bool StatIsDirty = false;


    public Vector2 MoveInput { get; private set; }
    public bool JumpInput { get; private set; }

    public Entity PlayerEntity { get; private set; }


    private InputSystem_Actions playerInput;

    public readonly Subject<Unit> OnInventoryKeyPerformed = new();
    public readonly Subject<Unit> OnReturnKeyPerformed    = new();


    protected override void OnAwake()
    {
        InitializeStats();
    }

    private void InitializeStats()
    {
        if (playerStatSO != null)
        {
            CurrentStats = new PlayerStat(playerStatSO);
            StatIsDirty = true;
        }
        else
        {
            Debug.LogError("PlayerStatSO가 할당되지 않았습니다! PlayerManager에 PlayerStatSO를 할당해주세요.");
        }
    }

    public void SetPlayerEntity(Entity entity)
    {
        PlayerEntity = entity;
    }

    void OnEnable()
    {
        playerInput ??= new InputSystem_Actions();
        playerInput.Enable();

        // 이동 입력 — performed(입력 시작/변경) / canceled(손 뗌)
        playerInput.Player.Move.performed += OnMovement;
        playerInput.Player.Move.canceled  += OnMovement;

        // 점프(제트팩) 입력 — started(누름) / canceled(손 뗌)
        playerInput.Player.Jump.started   += OnJump;
        playerInput.Player.Jump.canceled  += OnJump;

        // 인벤토리 / 귀환 키
        playerInput.Player.Inventory.performed += OnInventoryKey;
        playerInput.Player.Return.performed    += OnReturnKey;
    }

    void OnDisable()
    {
        if (playerInput == null) return;

        playerInput.Player.Move.performed -= OnMovement;
        playerInput.Player.Move.canceled  -= OnMovement;
        playerInput.Player.Jump.started   -= OnJump;
        playerInput.Player.Jump.canceled  -= OnJump;
        playerInput.Player.Inventory.performed -= OnInventoryKey;
        playerInput.Player.Return.performed    -= OnReturnKey;

        playerInput.Disable();
    }

    protected override void OnDestroy()
    {
        playerInput?.Dispose();
        playerInput = null;

        // Subject 완료 처리 (모든 구독자에게 스트림 종료 알림)
        OnInventoryKeyPerformed.OnCompleted();
        OnReturnKeyPerformed.OnCompleted();

        base.OnDestroy();
    }

    // --- InputAction 콜백 메서드 ---

    void OnMovement(InputAction.CallbackContext ctx)
        => MoveInput = ctx.canceled ? Vector2.zero : ctx.ReadValue<Vector2>();

    void OnJump(InputAction.CallbackContext ctx)
        => JumpInput = ctx.started;

    void OnInventoryKey(InputAction.CallbackContext ctx)
        => OnInventoryKeyPerformed.OnNext(Unit.Default);

    void OnReturnKey(InputAction.CallbackContext ctx)
        => OnReturnKeyPerformed.OnNext(Unit.Default);
}