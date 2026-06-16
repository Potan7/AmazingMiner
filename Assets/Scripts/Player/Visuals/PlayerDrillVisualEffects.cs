using UnityEngine;
using Unity.Entities;
using CoreDriller.Player.StatSystem;
using Unity.Transforms;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace CoreDriller.Player.DrillSystem
{
    public class PlayerDrillVisualEffects : MonoBehaviour
    {
        [System.Serializable]
        public struct VisualSettings
        {
            [ColorUsage(true, true)] public Color color;
            public float width;
        }

        [Header("Laser Beam (드릴 레이저)")]
        public VisualSettings laserBeam = new VisualSettings 
        { 
            color = new Color(0f, 1f, 1f, 2f), // 강한 네온 청록
            width = 0.07f 
        };

        [Header("Range Guide (드릴 최대 사거리 원)")]
        public VisualSettings rangeGuide = new VisualSettings 
        { 
            color = new Color(0f, 1f, 1f, 0.3f), // 은은한 반투명 청록
            width = 0.025f 
        };

        [Header("Explosion Guide (블록 캐지는 타격 반경 원)")]
        public VisualSettings explosionGuide = new VisualSettings 
        { 
            color = new Color(1f, 0.4f, 0f, 0.35f), // 반투명 네온 오렌지
            width = 0.02f 
        };

        [Header("Circle Quality")]
        [Range(12, 60)] public int circleSegments = 36; // 원을 그릴 정점 수

        // 내부 렌더러 참조
        private LineRenderer _laserRenderer;
        private LineRenderer _rangeRenderer;
        private LineRenderer _explosionRenderer;

        // ECS 관련 룩업 캐시
        private EntityManager _entityManager;
        private EntityQuery _drillQuery;
        private EntityQuery _playerQuery;

        void Start()
        {
            // 1. LineRenderer들을 소유할 자식 오브젝트 자동 구축 (원클릭 셋업 지원)
            SetupChildRenderers();

            // 2. 월드 활성화 대기 및 ECS 쿼리 통합 캐싱 (GC 가비지 0 최적화)
            InitializeECSAndStartLoop(destroyCancellationToken).Forget();
        }

        private void SetupChildRenderers()
        {
            var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            var defaultMat = unlitShader != null ? new Material(unlitShader) : new Material(Shader.Find("Sprites/Default"));

            // 1) 레이저 빔 자식 생성
            _laserRenderer = CreateLineRendererChild("LaserBeamEffect", laserBeam.color, laserBeam.width, defaultMat);
            _laserRenderer.positionCount = 2;
            _laserRenderer.loop = false;

            // 2) 사거리 원 가이드 자식 생성
            _rangeRenderer = CreateLineRendererChild("RangeGuideEffect", rangeGuide.color, rangeGuide.width, defaultMat);
            _rangeRenderer.positionCount = circleSegments + 1;
            _rangeRenderer.loop = true;

            // 3) 폭발 타격원 자식 생성
            _explosionRenderer = CreateLineRendererChild("ExplosionGuideEffect", explosionGuide.color, explosionGuide.width, defaultMat);
            _explosionRenderer.positionCount = circleSegments + 1;
            _explosionRenderer.loop = true;
        }

        private LineRenderer CreateLineRendererChild(string childName, Color color, float width, Material baseMat)
        {
            // 이미 존재한다면 기존 것 획득 (중복 생성 방지)
            Transform existingChild = transform.Find(childName);
            GameObject childObj = existingChild != null ? existingChild.gameObject : new GameObject(childName);
            
            if (existingChild == null)
            {
                childObj.transform.SetParent(transform);
                childObj.transform.localPosition = Vector3.zero;
                childObj.transform.localRotation = Quaternion.identity;
            }

            var lineRenderer = childObj.GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = childObj.AddComponent<LineRenderer>();
            }

            // 라인 매개변수 적용
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.material = baseMat;
            lineRenderer.useWorldSpace = true;
            lineRenderer.enabled = false;

            return lineRenderer;
        }

        private async UniTaskVoid InitializeECSAndStartLoop(CancellationToken token)
        {
            // 월드가 완성될 때까지 비동기 안전 대기
            await UniTask.WaitWhile(() => World.DefaultGameObjectInjectionWorld == null, cancellationToken: token);

            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // ECS 쿼리를 단 1회 통합 생성하여 사전 캐싱 (GC 가비지 발생 차단)
            _drillQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerDrillData>());
            _playerQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(), 
                ComponentType.ReadOnly<LocalTransform>()
            );

            // 단 하나의 통합 비동기 루틴만 실행! (CPU 부하를 3분의 1로 격감)
            UpdateVisualEffectsLoop(token).Forget();
        }

        private async UniTaskVoid UpdateVisualEffectsLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool isVisualActive = false;

                if (!_drillQuery.IsEmpty && !_playerQuery.IsEmpty)
                {
                    var drillData = _drillQuery.GetSingleton<PlayerDrillData>();
                    
                    var playerEntity = _playerQuery.GetSingletonEntity();
                    var playerTransform = _entityManager.GetComponentData<LocalTransform>(playerEntity);

                    Vector3 playerPos = playerTransform.Position;
                    float rangeRadius = drillData.DrillRange;
                    float explosionRadius = drillData.DrillExplosionRadius;

                    // 1. 최대 사거리 가이드 원 드로잉 (상시 은은하게 가동)
                    if (rangeRadius > 0f)
                    {
                        _rangeRenderer.enabled = true;
                        DrawCircle(_rangeRenderer, playerPos, rangeRadius);
                    }
                    else
                    {
                        _rangeRenderer.enabled = false;
                    }

                    // 2. 굴착 중일 때만 레이저빔 및 타격 삭감 원 활성화
                    if (drillData.IsActive)
                    {
                        isVisualActive = true;

                        Vector3 hitPos = new Vector3(drillData.LastHitPosition.x, drillData.LastHitPosition.y, playerPos.z);

                        // 2.1) 드릴 레이저 빔 그리기
                        _laserRenderer.enabled = true;
                        _laserRenderer.SetPosition(0, playerPos);
                        _laserRenderer.SetPosition(1, hitPos);

                        // 2.2) 삭감 반경 원 그리기
                        if (explosionRadius > 0f)
                        {
                            _explosionRenderer.enabled = true;
                            DrawCircle(_explosionRenderer, hitPos, explosionRadius);
                        }
                        else
                        {
                            _explosionRenderer.enabled = false;
                        }
                    }
                }

                if (!isVisualActive)
                {
                    _laserRenderer.enabled = false;
                    _explosionRenderer.enabled = false;
                }

                // 매 프레임 업데이트 스케줄링 (부드러운 카메라 및 마우스 추적 보장)
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        private void DrawCircle(LineRenderer renderer, Vector3 center, float radius)
        {
            // 36개 세그먼트를 순회하며 정점을 계산하여 세팅
            for (int i = 0; i <= circleSegments; i++)
            {
                float angle = i * (2f * Mathf.PI / circleSegments);
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;

                Vector3 pointPos = new Vector3(center.x + x, center.y + y, center.z);
                renderer.SetPosition(i, pointPos);
            }
        }
    }
}
