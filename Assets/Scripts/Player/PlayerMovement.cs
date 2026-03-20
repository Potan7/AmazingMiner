
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
    [UpdateBefore(typeof(MovementSystem))] // FixedStepSimulationSystemGroup이 아닌 특정 시스템보다 먼저 실행되게 명시하거나 삭제
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
                // MoveManager가 false로 만들더라도 InputManager에서 키를 누르고 있으면 
                // 강제로 다시 true가 되는 것을 방지하기 위해 이렇게 수정할 수도 있지만,
                // 제트팩처럼 계속 누를 때 유지되어야 한다면 이대로 팩트 적용을 유지합니다.
                movementInput.ValueRW.Jump = currentJump;
            }


        }
    }
}