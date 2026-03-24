using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoreDriller.Player.DrillSystem
{
    // 마우스 클릭 시 DigEvent를 생성하는 시스템 (메인 스레드 실행)
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PlayerInputDigSystem : SystemBase
    {
        private Camera mainCamera;

        protected override void OnCreate()
        {
            mainCamera = Camera.main;
            RequireForUpdate<PlayerTag>(); // PlayerTag가 존재할 때만 업데이트되도록 설정
        }

        protected override void OnUpdate()
        {
            if (!Mouse.current.leftButton.isPressed) return;

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            // 1. 매니저에서 현재 스탯(드릴 사거리, 범위, 데미지) 가져오기
            if (PlayerManager.Instance == null || PlayerManager.Instance.CurrentStats == null) return;
            var currentStats = PlayerManager.Instance.CurrentStats;

            // 2. 플레이어의 현재 위치 가져오기
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            if (!SystemAPI.HasComponent<LocalTransform>(playerEntity)) return;

            float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

            // 3. 마우스 위치를 월드 좌표로 변환하고 방향 계산
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            float2 targetPos2D = new float2(mousePos.x, mousePos.y);
            float2 playerPos2D = new float2(playerPos.x, playerPos.y);

            float2 direction = targetPos2D - playerPos2D;
            float distance = math.length(direction);

            // 4. 드릴 사거리(DrillRange) 제한 적용
            float2 hitPosition;
            if (distance > currentStats.DrillRange)
            {
                // 방향 벡터를 정규화(Normalize)하고 최대 사거리만큼만 뻗어나가도록 설정
                float2 normalizedDir = math.normalize(direction);
                hitPosition = playerPos2D + (normalizedDir * currentStats.DrillRange);
            }
            else
            {
                // 사거리 이내라면 마우스 위치 그대로 타격
                hitPosition = targetPos2D;
            }

            // 5. 이벤트 엔티티 생성
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new DigEvent
            {
                WorldPosition = hitPosition,
                Radius = currentStats.DrillRange * 0.6f, // 사거리에 비례해 타격 반경 조정(옵션) 혹은 0.6f 고정
                DigPower = currentStats.DrillPower
            });
        }
    }
}