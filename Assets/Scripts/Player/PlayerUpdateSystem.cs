
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using CoreDriller.Motion.Movement;

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

            // 3. 입력값을 컴포넌트에 업데이트
            if (SystemAPI.HasComponent<MovementInput>(playerEntity))
            {
                var movementInput = SystemAPI.GetComponentRW<MovementInput>(playerEntity);
                movementInput.ValueRW.Direction = currentMove;
                movementInput.ValueRW.Jump = currentJump;
            }

            // 이하는 스탯 업데이트
            if (!PlayerManager.Instance.StatIsDirty) return;


            if (SystemAPI.HasComponent<MovementStats>(playerEntity))
            {
                var movementStats = SystemAPI.GetComponentRW<MovementStats>(playerEntity);
                movementStats.ValueRW.MoveSpeed = currentStats.MoveSpeed;
                movementStats.ValueRW.JumpForce = currentStats.JetpackThrust;
            }

            PlayerManager.Instance.StatIsDirty = false; // ?�데?�트 ?�료 ???�티 ?�래�?리셋
        }
    }
}
