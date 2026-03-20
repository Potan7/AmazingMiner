using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoreDriller.Player
{

    public class PlayerInputManager : MonoBehaviour
    {
        public static PlayerInputManager Instance { get; private set; }

        InputSystem_Actions playerInput;

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
            playerInput.Disable();
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
                playerInput.Player.Jump.started -= OnJump;
                playerInput.Player.Jump.canceled -= OnJump;
                playerInput.Dispose();
                playerInput = null;
            }
        }

        void OnMovement(InputAction.CallbackContext context)
        {
            var inputVector = context.ReadValue<Vector2>();
            MoveInput = inputVector;
            // Debug.Log($"[PlayerInputManager] Move Input: {MoveInput}");
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

            // Debug.Log($"[PlayerInputManager] Jump Input: {JumpInput}");
        }
    }
}