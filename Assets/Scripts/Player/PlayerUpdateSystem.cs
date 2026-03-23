
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using CoreDriller.Motion.Movement;

namespace CoreDriller.Player
{
    // MonoBehaviour??PlayerManager ?°ì´?°ë? ECS ì»´í¬?ŒíŠ¸ë¡??„ë‹¬?˜ëŠ” ë¸Œë¦¿ì§€ ??• ???´ë‹¹?©ë‹ˆ??
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PlayerUpdateSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerTag>(); // PlayerTagê°€ ?ˆëŠ” ?”í‹°?°ê? ì¡´ì¬???Œë§Œ ?œìŠ¤?œì´ ?…ë°?´íŠ¸?˜ë„ë¡??¤ì •
        }

        protected override void OnUpdate()
        {
            // 1. ?±ê???ë§¤ë‹ˆ?€ê°€ ë©”ëª¨ë¦¬ì— ì¤€ë¹„ë˜?ˆëŠ”ì§€ ?•ì¸
            if (PlayerManager.Instance == null || PlayerManager.Instance.CurrentStats == null) return;
            if (!SystemAPI.HasSingleton<PlayerTag>()) return;

            // 2. ë§??„ë ˆ??ë§¤ë‹ˆ?€(?¸í’‹ & ?íƒœ)?ì„œ ìµœì‹  ê°’ì„ ?½ì–´?µë‹ˆ??
            Vector2 currentMove = PlayerManager.Instance.MoveInput;
            bool currentJump = PlayerManager.Instance.JumpInput;
            var currentStats = PlayerManager.Instance.CurrentStats;

            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();

            // 3. ?…ë ¥ ?íƒœ ?…ë°?´íŠ¸
            if (SystemAPI.HasComponent<MovementInput>(playerEntity))
            {
                var movementInput = SystemAPI.GetComponentRW<MovementInput>(playerEntity);
                movementInput.ValueRW.Direction = currentMove;
                movementInput.ValueRW.Jump = currentJump;
            }

            // ?´í•˜ë¡œëŠ” ?¤íƒ¯ ?…ë°?´íŠ¸
            if (!PlayerManager.Instance.StatIsDirty) return;

            // 4. ?¤íƒ¯ ?íƒœ ?…ë°?´íŠ¸ (ì±„êµ´, ?´ë™ ?ë„ ??
            if (SystemAPI.HasComponent<MovementStats>(playerEntity))
            {
                var movementStats = SystemAPI.GetComponentRW<MovementStats>(playerEntity);
                movementStats.ValueRW.MoveSpeed = currentStats.MoveSpeed;
                movementStats.ValueRW.JumpForce = currentStats.JetpackThrust;
            }

            PlayerManager.Instance.StatIsDirty = false; // ?…ë°?´íŠ¸ ?„ë£Œ ???”í‹° ?Œë˜ê·?ë¦¬ì…‹
        }
    }
}
