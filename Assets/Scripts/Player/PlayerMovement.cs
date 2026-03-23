
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using CoreDriller.Movement;

namespace CoreDriller.Player
{
    // 1. Authoring: Inspector에서 Movement 속성을 설정하고 ECS 컴포넌트로 변환
    public class PlayerMovement : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float jumpForce = 10f;

        class PlayerMovementBaker : Baker<PlayerMovement>
        {
            public override void Bake(PlayerMovement authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MovementInput());
                AddComponent(entity, new MovementStats
                {
                    MoveSpeed = authoring.moveSpeed,
                    JumpForce = authoring.jumpForce
                });
            }
        }
    }

    // 2. System: PlayerInputManager의 이벤트를 읽어서 ECS의 MovementInput으로 전달
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PlayerInputSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // 1. 싱글톤이 아직 없으면 이번 프레임은 스킵 (안전 장치)
            if (PlayerInputManager.Instance == null) return;
            if (!SystemAPI.HasSingleton<PlayerTag>()) return;

            // 2. 매 프레임 InputManager의 최신 값을 직접 폴링해옵니다.
            // (PlayerInputManager에 MoveInput, JumpInput 프로퍼티가 있다고 가정합니다)
            Vector2 currentMove = PlayerInputManager.Instance.MoveInput;
            bool currentJump = PlayerInputManager.Instance.JumpInput;

            var player = SystemAPI.GetSingletonEntity<PlayerTag>();

            if (SystemAPI.HasComponent<MovementInput>(player))
            {
                var movementInput = SystemAPI.GetComponentRW<MovementInput>(player);
                movementInput.ValueRW.Direction = currentMove;
                movementInput.ValueRW.Jump = currentJump;
            }


        }
    }
}