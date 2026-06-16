using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using CoreDriller;

namespace CoreDriller.General
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class MouseInputSystem : SystemBase
    {
        private Camera _mainCamera;
        private EntityQuery _inputQuery; // 쿼리를 미리 캐싱

        protected override void OnCreate()
        {
            // 싱글톤 초기화
            if (!SystemAPI.HasSingleton<MouseInputData>())
            {
                EntityManager.CreateSingleton(new MouseInputData());
            }

            // 쿼리 미리 생성 (성능 향상)
            _inputQuery = GetEntityQuery(typeof(MouseInputData));
        }

        protected override void OnUpdate()
        {
            // 1. 카메라 캐싱 최적화
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            // 2. 입력 값 읽기 (변수 할당 최소화)
            var mouse = Mouse.current;
            if (mouse == null) return;

            bool isPressed = mouse.leftButton.isPressed;
            
            // 마우스가 UI 위에 있을 때는 게임 월드 클릭(채굴) 판정을 무시합니다.
            if (isPressed && UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                isPressed = false;
            }

            float2 worldPos = float2.zero;

            if (isPressed)
            {
                // ScreenToWorldPoint 호출 시 z값을 0이 아닌 카메라와의 거리로 주는 것이 정확할 때가 많습니다.
                Vector3 screenPos = mouse.position.ReadValue();
                Vector3 worldPos3D = _mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -_mainCamera.transform.position.z));
                worldPos = new float2(worldPos3D.x, worldPos3D.y);
            }

            // 3. SetSingleton 대신 직접 쓰기 (RW 참조 사용)
            _inputQuery.GetSingletonRW<MouseInputData>().ValueRW = new MouseInputData
            {
                IsLeftClickPressed = isPressed,
                WorldPosition = worldPos
            };
            // SystemAPI.GetSingletonRW<MouseInputData>().ValueRW = new MouseInputData
            // {
            //     IsLeftClickPressed = isPressed,
            //     WorldPosition = worldPos
            // };
        }
    }
}