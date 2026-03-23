using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    public PlayerStatSO playerStatSO; // 에디터에서 설정할 수 있는 ScriptableObject 참조
    public PlayerStat CurrentStats { get; private set; } // 게임 내에서 실제로 사용되는 플레이어 스탯 인스턴스

    public bool StatIsDirty = false;

    private InputSystem_Actions playerInput;

    public Vector2 MoveInput { get; private set; }
    public bool JumpInput { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 게임 시작 시 초기값 설정 
        // (추후 세이브 기능이 구현되면, 세이브 데이터를 우선적으로 불러와 CurrentStats에 할당하면 됩니다)
        if (playerStatSO != null)
        {
            CurrentStats = new PlayerStat(playerStatSO);
            StatIsDirty = true; // 초기값 설정 후 다른 시스템에서 이 값을 읽어갈 수 있도록 더티 플래그 설정
        }
        else
        {
            Debug.LogError("PlayerStatSO가 할당되지 않았습니다! PlayerManager에 PlayerStatSO를 할당해주세요.");
        }
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
            playerInput.Dispose();
            playerInput = null;
        }
    }

    void OnMovement(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
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