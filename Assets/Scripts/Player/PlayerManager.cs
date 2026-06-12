using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using CoreDriller.Player.StatSystem;

[DefaultExecutionOrder(-100)] // 다른 시스템보다 먼저 실행되도록 설정 (필요에 따라 조정 가능)
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    public PlayerStatSO playerStatSO; // 에디터에서 설정할 수 있는 ScriptableObject 참조
    public PlayerStat CurrentStats { get; private set; } // 게임 내에서 실제로 사용되는 플레이어 스탯 인스턴스

    public bool StatIsDirty = false;


    public Vector2 MoveInput { get; private set; }
    public bool JumpInput { get; private set; }

    public Entity PlayerEntity { get; private set; }


    private InputSystem_Actions playerInput;
    public event Action<InputAction.CallbackContext> OnInventoryKeyPerformed;


    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitStat();
    }

    void InitStat()
    {
        // 게임 시작 시 초기값 설정 
        // TODO: 세이브 기능이 구현되면, 세이브 데이터를 우선적으로 불러와 CurrentStats에 할당하기
        if (playerStatSO != null)
        {
            CurrentStats = new PlayerStat(playerStatSO);
            StatIsDirty = true; // 초기값 설정 후 다른 시스템에서 이 값을 읽어갈 수 있도록 더티 플래그 설정
        }
        else
        {
            Debug.LogError("PlayerStatSO가 할당되지 않았습니다! PlayerManager에 PlayerStatSO를 할당해주세요.");
            return;
        }

        // PlayerStat으로 ECS 컴포넌트들을 가진 엔티티를 생성합니다.
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        Entity statEntity = entityManager.CreateEntity();

        // 각각의 컴포넌트에 PlayerStat에서 읽은 값을 할당하여 엔티티에 추가합니다.
        entityManager.AddComponentData(statEntity, new PlayerDrillData
        {
            DrillPower = CurrentStats.DrillPower,
            DrillSpeed = CurrentStats.DrillSpeed,
            DrillFuelConsumption = CurrentStats.DrillFuelConsumption,
            DigCooldown = CurrentStats.DigCooldown,
            DrillRange = CurrentStats.DrillRange,
            DrillExplosionRadius = CurrentStats.DrillExplosionRadius
        });

        entityManager.AddComponentData(statEntity, new PlayerMovementData
        {
            MoveSpeed = CurrentStats.MoveSpeed,
            MaxFuel = CurrentStats.MaxFuel,
            CurrentFuel = CurrentStats.MaxFuel,
            JetpackThrust = CurrentStats.JetpackThrust,
            JetpackFuelConsumption = CurrentStats.JetpackFuelConsumption
        });

        entityManager.AddComponentData(statEntity, new PlayerHealthData
        {
            MaxHealth = CurrentStats.MaxHealth,
            CurrentHealth = CurrentStats.MaxHealth
        });

        entityManager.AddComponentData(statEntity, new PlayerInventoryData
        {
            InventorySlotSize = CurrentStats.InventorySlotSize,
            InventorySlotCount = CurrentStats.InventorySlotCount,
            ItemPickupRange = CurrentStats.ItemPickupRange
        });

        // 인벤토리 버퍼 추가 및 빈 슬롯으로 초기화
        entityManager.AddBuffer<InventoryBuffer>(statEntity);
        var invBuffer = entityManager.GetBuffer<InventoryBuffer>(statEntity);
        for (int i = 0; i < CurrentStats.InventorySlotCount; i++)
        {
            invBuffer.Add(new InventoryBuffer { ItemType = 0, Count = 0 });
        }

        PlayerEntity = statEntity;
    }

    private void Start()
    {
        // 플레이어 UI에 초기 스탯 값 전달
        PlayerUIEvents.TriggerFuelChanged(CurrentStats.MaxFuel, CurrentStats.MaxFuel);
        PlayerUIEvents.TriggerHealthChanged(CurrentStats.MaxHealth, CurrentStats.MaxHealth);
        PlayerUIEvents.TriggerDrillStateChanged(0f, false);
        PlayerUIEvents.TriggerInventoryChanged();
    }

    void OnEnable()
    {
        if (playerInput == null)
        {
            playerInput = new InputSystem_Actions();

            playerInput.Player.Move.performed += OnMovement;
            playerInput.Player.Move.canceled += OnMovement;
            playerInput.Player.Jump.started += OnJump;
            playerInput.Player.Jump.canceled += OnJump;
            playerInput.Player.Inventory.performed += OnInventoryKey;
        }
        playerInput.Enable();
    }

    void OnDisable()
    {
        playerInput?.Disable();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (playerInput != null)
        {
            playerInput.Player.Move.performed -= OnMovement;
            playerInput.Player.Move.canceled -= OnMovement;
            playerInput.Player.Jump.started -= OnJump;
            playerInput.Player.Jump.canceled -= OnJump;
            playerInput.Player.Inventory.performed -= OnInventoryKey;
            playerInput.Dispose();
            playerInput = null;
        }
    }

    void OnMovement(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    void OnInventoryKey(InputAction.CallbackContext context)
    {
        OnInventoryKeyPerformed?.Invoke(context);
    }

    void OnJump(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            JumpInput = true;
        }
        else if (context.canceled)
        {
            JumpInput = false;
        }
    }
}