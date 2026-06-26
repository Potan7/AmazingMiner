
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using CoreDriller.Motion.Movement;
using CoreDriller.Player.StatSystem;

namespace CoreDriller.Player
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PlayerUpdateSystem : SystemBase
    {

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerTag>();
        }

        protected override void OnUpdate()
        {
            // 1. 매니저와 싱글톤 존재 여부 확인
            if (PlayerManager.Instance == null || PlayerManager.Instance.CurrentStats == null) return;
            if (!SystemAPI.HasSingleton<PlayerTag>()) return;

            // 2. 매니저에서 현재 입력값과 스탯 가져오기
            Vector2 currentMove = PlayerManager.Instance.MoveInput;
            bool currentJump = PlayerManager.Instance.JumpInput;
            var currentStats = PlayerManager.Instance.CurrentStats;

            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            float deltaTime = SystemAPI.Time.DeltaTime;

            // 3. 연료 소모 및 제트팩/드릴 구동 통제
            bool canJump = currentJump;
            if (SystemAPI.TryGetSingletonRW<PlayerMovementData>(out var movementData))
            {
                // 제트팩 비행 시 연료 소모
                if (currentJump && movementData.ValueRO.CurrentFuel > 0f)
                {
                    movementData.ValueRW.CurrentFuel = math.max(0f, movementData.ValueRW.CurrentFuel - movementData.ValueRO.JetpackFuelConsumption * deltaTime);
                }

                // 드릴 굴착 시 연료 소모 (마우스 왼쪽 클릭 중)
                if (SystemAPI.TryGetSingleton<MouseInputData>(out var mouseInput) && mouseInput.IsLeftClickPressed && movementData.ValueRO.CurrentFuel > 0f)
                {
                    if (SystemAPI.TryGetSingleton<PlayerDrillData>(out var drillData))
                    {
                        movementData.ValueRW.CurrentFuel = math.max(0f, movementData.ValueRW.CurrentFuel - drillData.DrillFuelConsumption * deltaTime);
                    }
                }

                // 연료 유무에 따른 점프 가능 여부 설정
                canJump = currentJump && movementData.ValueRO.CurrentFuel > 0f;
            }

            // 4. 입력값을 컴포넌트에 업데이트
            if (SystemAPI.HasComponent<MovementInput>(playerEntity))
            {
                var movementInput = SystemAPI.GetComponentRW<MovementInput>(playerEntity);
                movementInput.ValueRW.Direction = currentMove;
                movementInput.ValueRW.Jump = canJump;
            }

            // 스탯 업데이트
            if (PlayerManager.Instance.statIsDirty)
            {
                if (SystemAPI.HasComponent<MovementStats>(playerEntity))
                {
                    var movementStats = SystemAPI.GetComponentRW<MovementStats>(playerEntity);
                    movementStats.ValueRW.MoveSpeed = currentStats.MoveSpeed;
                    movementStats.ValueRW.JumpForce = currentStats.JetpackThrust;
                }

                PlayerManager.Instance.statIsDirty = false;
            }
        }
    }
}
