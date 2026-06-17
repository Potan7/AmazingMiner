using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using CoreDriller.Player.StatSystem;
using CoreDriller.Player;
using Potan.CoreUtils;
using Cysharp.Threading.Tasks;

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
    public event Action OnInventoryKeyPerformed;
    public event Action OnReturnKeyPerformed;


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
        if (playerInput == null)
        {
            playerInput = new InputSystem_Actions();

            playerInput.Player.Move.performed += OnMovement;
            playerInput.Player.Move.canceled += OnMovement;
            playerInput.Player.Jump.started += OnJump;
            playerInput.Player.Jump.canceled += OnJump;
            playerInput.Player.Inventory.performed += OnInventoryKey;
            playerInput.Player.Return.performed += OnReturnKey;
        }
        playerInput.Enable();
    }

    void OnDisable()
    {
        playerInput?.Disable();
    }

    protected override void OnDestroy()
    {
        if (playerInput != null)
        {
            playerInput.Player.Move.performed -= OnMovement;
            playerInput.Player.Move.canceled -= OnMovement;
            playerInput.Player.Jump.started -= OnJump;
            playerInput.Player.Jump.canceled -= OnJump;
            playerInput.Player.Inventory.performed -= OnInventoryKey;
            playerInput.Player.Return.performed -= OnReturnKey;
            playerInput.Dispose();
            playerInput = null;
        }
        base.OnDestroy();
    }

    void OnMovement(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    void OnInventoryKey(InputAction.CallbackContext context)
    {
        OnInventoryKeyPerformed?.Invoke();
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

    void OnReturnKey(InputAction.CallbackContext context)
    {
        OnReturnKeyPerformed?.Invoke();
    }
}