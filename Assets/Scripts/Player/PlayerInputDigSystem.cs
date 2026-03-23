using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoreDriller.Player
{
    // 마우스 클릭 시 DigEvent를 생성하는 시스템 (메인 스레드 실행)
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PlayerInputDigSystem : SystemBase
    {
        private Camera mainCamera;

        protected override void OnCreate()
        {
            mainCamera = Camera.main;
        }

        protected override void OnUpdate()
        {
            if (!Mouse.current.leftButton.isPressed) return; // 꾹 누르고 있는 드릴 형태 지원을 위해 isPressed 사용

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());

            // 이벤트 엔티티 생성! (드릴 기준)
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new DigEvent
            {
                WorldPosition = new float2(mousePos.x, mousePos.y),
                Radius = 0.6f,   // 마우스 클릭 반경 (0.5f면 딱 블록 하나, 조금 여유를 줌)
                DigPower = 100f  // 파괴력
            });
        }
    }
}