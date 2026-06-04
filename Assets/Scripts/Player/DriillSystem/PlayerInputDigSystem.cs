using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.LowLevelPhysics2D;
using CoreDriller.Player.StatSystem;

namespace CoreDriller.Player.DrillSystem
{
    // 마우스 클릭 시 DigEvent를 생성하는 시스템 (메인 스레드 실행)
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PlayerInputDigSystem : ISystem
    {
        public PhysicsQuery.QueryFilter drillQueryFilter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>(); // PlayerTag가 존재할 때만 업데이트되도록 설정

            var mask = PhysicsMask.All;
            mask.ResetBit(3);
            drillQueryFilter = new PhysicsQuery.QueryFilter(PhysicsMask.One, mask);

        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 1. 드릴 데이터 싱글톤 확보
            if (!SystemAPI.TryGetSingleton<PlayerDrillData>(out var playerDrillData)) return;

            float deltaTime = SystemAPI.Time.DeltaTime;

            // 드릴 쿨타임 타이머 감산 처리
            if (playerDrillData.CurrentTimer > 0f)
            {
                playerDrillData.CurrentTimer = math.max(0f, playerDrillData.CurrentTimer - deltaTime);
            }

            // 마우스 클릭 및 연료 상태 검사
            bool isDiggingAttempt = SystemAPI.TryGetSingleton<MouseInputData>(out var mouseInput) && mouseInput.IsLeftClickPressed;
            bool hasFuel = !SystemAPI.TryGetSingleton<PlayerMovementData>(out var movementData) || movementData.CurrentFuel > 0f;

            if (!isDiggingAttempt || !hasFuel)
            {
                // 굴착을 시도하지 않거나 연료가 없는 경우 IsActive = false로 리셋
                if (playerDrillData.IsActive)
                {
                    playerDrillData.IsActive = false;
                    SystemAPI.SetSingleton(playerDrillData);
                }
                
                // 만약 타이머 갱신만 필요했다면 업데이트 처리
                if (playerDrillData.CurrentTimer > 0f)
                {
                    SystemAPI.SetSingleton(playerDrillData);
                }
                return;
            }

            // 쿨타임 대기 중에도 플레이어는 마우스를 누르고 굴착을 시도하므로 IsActive는 계속 유지
            playerDrillData.IsActive = true;

            // 2. 플레이어 위치 가져오기 (SingletonEntity 활용)
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
            float2 playerPosition = playerTransform.Position.xy; // .xy 스위즐링 활용

            float2 direction = mouseInput.WorldPosition - playerPosition;
            float distance = math.length(direction);

            // 3. 레이캐스트 설정
            float2 normalizedDir = distance > 1e-5f ? math.normalize(direction) : new float2(1, 0);
            float raycastDist = math.min(distance, playerDrillData.DrillRange);

            var rayInput = new PhysicsQuery.CastRayInput(playerPosition, normalizedDir * raycastDist);

            // 4. 레이캐스트 실행
            var hits = PhysicsWorld.defaultWorld.CastRay(rayInput, drillQueryFilter);
            float2 hitPosition = (hits.Length > 0) ? hits[0].point : playerPosition + (normalizedDir * raycastDist);
            hits.Dispose();

            // 최신 타격 위치를 실시간 기록하여 이펙트 스크립트가 선 끝을 꽂을 수 있게 갱신
            playerDrillData.LastHitPosition = hitPosition;

            // 쿨타임이 다 찼을 때에만 실제 채굴 이벤트 발송
            if (playerDrillData.CurrentTimer <= 0f)
            {
                // 쿨타임 리셋
                playerDrillData.CurrentTimer = playerDrillData.DigCooldown;

                var bufferEntity = SystemAPI.GetSingletonEntity<DigEvent>();
                var digBuffer = SystemAPI.GetBuffer<DigEvent>(bufferEntity);

                digBuffer.Add(new DigEvent
                {
                    WorldPosition = hitPosition,
                    Radius = playerDrillData.DrillExplosionRadius,
                    DigPower = playerDrillData.DrillPower,
                    DigSpeed = playerDrillData.DrillSpeed,
                    DeltaTime = SystemAPI.Time.DeltaTime
                });
            }

            // 최종 드릴 싱글톤 데이터 업데이트
            SystemAPI.SetSingleton(playerDrillData);

            // // 5. ECB를 사용하여 엔티티 생성 (성능 최적화)
            // var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            //                    .CreateCommandBuffer(state.WorldUnmanaged);

            // var entity = ecb.CreateEntity();
            // ecb.AddComponent(entity, new DigEvent
            // {
            //     WorldPosition = hitPosition,
            //     Radius = playerDrillData.DrillExplosionRadius,
            //     DigPower = playerDrillData.DrillPower
            // });
        }
    }
}