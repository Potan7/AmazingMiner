using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using CoreDriller.Player.StatSystem;
using CoreDriller.Player;
using Potan.CoreUtils;
using Cysharp.Threading.Tasks;
using R3;
using Unity.Collections;
using System;
using UnityEngine.Serialization;

[DefaultExecutionOrder(-100)] // 다른 시스템보다 먼저 실행되도록 설정 (필요에 따라 조정 가능)
public class PlayerManager : MonoSingleton<PlayerManager>
{

    [FormerlySerializedAs("playerStatSO")] public PlayerStatSO playerStatSo; // 에디터에서 설정할 수 있는 ScriptableObject 참조
    public PlayerStat CurrentStats { get; private set; } // 게임 내에서 실제로 사용되는 플레이어 스탯 인스턴스

    public bool statIsDirty = false;


    public Vector2 MoveInput { get; private set; }
    public bool JumpInput { get; private set; }

    public Entity PlayerEntity { get; private set; }


    private InputSystem_Actions _playerInput;

    public readonly Subject<Unit> OnInventoryKeyPerformed = new();
    public readonly Subject<Unit> OnReturnKeyPerformed    = new();
    public readonly Subject<Unit> OnInteractKeyPerformed   = new();
    public readonly Subject<Vector2> OnMouseWheelScrolled = new();


    protected override void OnAwake()
    {
        InitializeStats();
    }

    private void InitializeStats()
    {
        if (playerStatSo != null)
        {
            CurrentStats = new PlayerStat(playerStatSo);
            statIsDirty = true;
        }
        else
        {
            Debug.LogError("PlayerStatSO가 할당되지 않았습니다! PlayerManager에 PlayerStatSO를 할당해주세요.");
        }
    }

    /// <summary>플레이어 Entity가 베이킹 완료된 후 호출. Entity 참조를 저장하고 현재 스탯을 ECS로 업로드.</summary>
    public void SetPlayerEntity(Entity entity)
    {
        PlayerEntity = entity;
        UploadStatsToEntity();
    }

    /// <summary>지하 진입 시: PlayerManager의 현재 런타임 스탯을 ECS 컴포넌트에 업로드.</summary>
    private void UploadStatsToEntity()
    {
        if (CurrentStats == null) return;
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (!em.Exists(PlayerEntity)) return;

        // 체력 업로드
        if (em.HasComponent<PlayerHealthData>(PlayerEntity))
        {
            em.SetComponentData(PlayerEntity, new PlayerHealthData
            {
                MaxHealth     = CurrentStats.MaxHealth,
                CurrentHealth = CurrentStats.CurrentHealth,
            });
        }

        // 연료 / 이동 업로드
        if (em.HasComponent<PlayerMovementData>(PlayerEntity))
        {
            var existing = em.GetComponentData<PlayerMovementData>(PlayerEntity);
            existing.MaxFuel     = CurrentStats.MaxFuel;
            existing.CurrentFuel = CurrentStats.CurrentFuel;
            em.SetComponentData(PlayerEntity, existing);
        }

        // 인벤토리 업로드
        if (em.HasBuffer<InventoryBuffer>(PlayerEntity))
        {
            var buffer = em.GetBuffer<InventoryBuffer>(PlayerEntity);
            buffer.Clear();
            foreach (var (itemType, count) in CurrentStats.InventorySnapshot)
            {
                buffer.Add(new InventoryBuffer { ItemType = itemType, Count = count });
            }
        }

        Debug.Log($"[PlayerManager] ECS 스탯 업로드 완료 — 체력: {CurrentStats.CurrentHealth}/{CurrentStats.MaxHealth}, 연료: {CurrentStats.CurrentFuel}/{CurrentStats.MaxFuel}, 인벤토리: {CurrentStats.InventorySnapshot.Count}종");
    }

    /// <summary>귀환 시: ECS 컴포넌트의 현재값을 PlayerManager로 저장. PlayerReturnSystem에서 씬 전환 직전 호출.</summary>
    public void SaveStatsFromEntity()
    {
        if (CurrentStats == null) return;
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (!em.Exists(PlayerEntity)) return;


        // 체력 저장
        if (em.HasComponent<PlayerHealthData>(PlayerEntity))
        {
            var health = em.GetComponentData<PlayerHealthData>(PlayerEntity);
            CurrentStats.CurrentHealth = health.CurrentHealth;
        }

        // 연료 저장
        if (em.HasComponent<PlayerMovementData>(PlayerEntity))
        {
            var movement = em.GetComponentData<PlayerMovementData>(PlayerEntity);
            CurrentStats.CurrentFuel = movement.CurrentFuel;
        }

        // 인벤토리 저장
        if (em.HasBuffer<InventoryBuffer>(PlayerEntity))
        {
            var buffer = em.GetBuffer<InventoryBuffer>(PlayerEntity);
            CurrentStats.InventorySnapshot.Clear();
            for (int i = 0; i < buffer.Length; i++)
            {
                CurrentStats.InventorySnapshot.Add((buffer[i].ItemType, buffer[i].Count));
            }
        }

        PlayerEntity = Entity.Null;
        DevLog.Log($"ECS 스탯 저장 완료 — 체력: {CurrentStats.CurrentHealth}/{CurrentStats.MaxHealth}, 연료: {CurrentStats.CurrentFuel}/{CurrentStats.MaxFuel}", this);
    }

    private void OnEnable()
    {
        _playerInput ??= new InputSystem_Actions();
        _playerInput.Enable();

        // 이동 입력 — performed(입력 시작/변경) / canceled(손 뗌)
        _playerInput.Player.Move.performed += OnMovement;
        _playerInput.Player.Move.canceled  += OnMovement;

        // 점프(제트팩) 입력 — started(누름) / canceled(손 뗌)
        _playerInput.Player.Jump.started   += OnJump;
        _playerInput.Player.Jump.canceled  += OnJump;

        // 인벤토리 / 귀환 키
        _playerInput.Player.Inventory.performed += OnInventoryKey;
        _playerInput.Player.Return.performed    += OnReturnKey;

        _playerInput.Player.Interact.performed += OnInteractPerformed;
        
        _playerInput.UI.ScrollWheel.performed += OnMouseWheel;
    }

    private void OnDisable()
    {
        if (_playerInput == null) return;

        _playerInput.Player.Move.performed -= OnMovement;
        _playerInput.Player.Move.canceled  -= OnMovement;
        _playerInput.Player.Jump.started   -= OnJump;
        _playerInput.Player.Jump.canceled  -= OnJump;
        _playerInput.Player.Inventory.performed -= OnInventoryKey;
        _playerInput.Player.Return.performed    -= OnReturnKey;
        
        _playerInput.Player.Interact.performed -= OnInteractPerformed;

        _playerInput.UI.ScrollWheel.performed -= OnMouseWheel;

        _playerInput.Disable();
    }

    protected override void OnDestroy()
    {
        _playerInput?.Dispose();
        _playerInput = null;

        // Subject 완료 처리 (모든 구독자에게 스트림 종료 알림)
        OnInventoryKeyPerformed.OnCompleted();
        OnReturnKeyPerformed.OnCompleted();

        base.OnDestroy();
    }

    // --- InputAction 콜백 메서드 ---

    private void OnMovement(InputAction.CallbackContext ctx)
        => MoveInput = ctx.canceled ? Vector2.zero : ctx.ReadValue<Vector2>();

    private void OnJump(InputAction.CallbackContext ctx)
        => JumpInput = ctx.started;

    private void OnInventoryKey(InputAction.CallbackContext ctx)
        => OnInventoryKeyPerformed.OnNext(Unit.Default);

    private void OnReturnKey(InputAction.CallbackContext ctx)
        => OnReturnKeyPerformed.OnNext(Unit.Default);

    private void OnMouseWheel(InputAction.CallbackContext ctx)
    {
        var scrollValue = ctx.ReadValue<Vector2>();
        OnMouseWheelScrolled.OnNext(scrollValue);
    }
    
    private void OnInteractPerformed(InputAction.CallbackContext obj) 
        => OnInteractKeyPerformed.OnNext(Unit.Default);

    public void TogglePlayerInput(bool isEnabled)
    {
        if (_playerInput == null) return;

        if (isEnabled)
        {
            _playerInput.Player.Enable();
        }
        else
        {
            _playerInput.Player.Disable();
            MoveInput = Vector2.zero;
            JumpInput = false;
        }
    }
}
