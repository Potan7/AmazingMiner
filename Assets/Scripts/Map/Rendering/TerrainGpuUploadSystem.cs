using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Rendering;
using Unity.Entities.Graphics;
using System;

namespace CoreDriller.Map.Rendering
{

    // 1. 관리형 컴포넌트(Class): Mesh 객체의 참조를 들고 있어 나중에 메모리를 해제할 수 있게 합니다.
    public class ChunkProceduralMesh : IComponentData, IDisposable
    {
        public Mesh GeneratedMesh;

        public void Dispose()
        {
            if (GeneratedMesh != null)
            {
                UnityEngine.Object.Destroy(GeneratedMesh);
                GeneratedMesh = null;
            }
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TerrainGpuUploadSystem : SystemBase
    {
        private EntityQuery _updateQuery;
        private Material _terrainMaterial;

        protected override void OnCreate()
        {
            _updateQuery = GetEntityQuery(
                ComponentType.ReadOnly<MeshNeedsUpdateTag>(),
                ComponentType.ReadOnly<ChunkComponent>(),
                ComponentType.ReadOnly<ChunkVertex>(),
                ComponentType.ReadOnly<ChunkTriangle>()
                );

            // 에셋 로드
            _terrainMaterial = Resources.Load<Material>("TerrainMaterial");
            RequireForUpdate(_updateQuery);
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 쿼리로 업데이트가 필요한 엔티티들을 배열로 가져옵니다. 
            // 직접 SystemAPI.Query 로 루프를 돌면서 EntityManager 로 구조적 변경(AddComponent 등)을 하면
            // "Structural changes are not allowed while iterating over entities" 에러가 발생합니다.
            using var entityArray = _updateQuery.ToEntityArray(Allocator.TempJob);

            foreach (var entity in entityArray)
            {
                var vertices = EntityManager.GetBuffer<ChunkVertex>(entity);
                var triangles = EntityManager.GetBuffer<ChunkTriangle>(entity);

                // 1. 데이터가 비어있으면 스킵
                if (vertices.Length == 0 || triangles.Length == 0)
                {
                    ecb.RemoveComponent<MeshNeedsUpdateTag>(entity);
                    continue;
                }

                // 구조적 변경을 하기 전에 버퍼의 내용을 미리 배열에 복사해둡니다.
                // EntityManager.AddComponentData()나 RenderMeshUtility.AddComponents()를 호출하면 
                // 기존 버퍼의 메모리 참조가 무효화되어 그 아래에서 접근하면 ObjectDisposedException 이 발생하기 때문입니다.
                var posArray = new Vector3[vertices.Length];
                for (int i = 0; i < vertices.Length; i++) posArray[i] = vertices[i].Position;

                var indexArray = new int[triangles.Length];
                for (int i = 0; i < triangles.Length; i++) indexArray[i] = triangles[i].Value;


                Mesh targetMesh;

                // 2. 기존 Mesh가 있는지 확인하고 재사용 (메모리 최적화)
                if (EntityManager.HasComponent<ChunkProceduralMesh>(entity))
                {
                    targetMesh = EntityManager.GetComponentData<ChunkProceduralMesh>(entity).GeneratedMesh;
                    targetMesh.Clear(); // 기존 데이터 비우기
                }
                else
                {
                    // 없으면 새로 생성 후 관리형 컴포넌트로 엔티티에 부착
                    targetMesh = new Mesh();
                    targetMesh.MarkDynamic(); // 런타임에 자주 변할 것임을 엔진에 알림 (성능 최적화)

                    // EntityManager를 통해 관리형 컴포넌트 직접 추가 (구조적 변경 발생 지점)
                    EntityManager.AddComponentData(entity, new ChunkProceduralMesh { GeneratedMesh = targetMesh });
                }

                // 3. CPU 버퍼 데이터를 Mesh에 밀어넣기 (미리 복사해둔 배열 사용)
                targetMesh.SetVertices(posArray);
                targetMesh.SetTriangles(indexArray, 0);
                targetMesh.RecalculateNormals(); // 조명을 받기 위해 노멀 계산
                targetMesh.RecalculateBounds();

                // 4. Entities Graphics 시스템에 메쉬 렌더링 등록
                // RenderMeshUtility가 ECS 환경에 필요한 모든 렌더링 컴포넌트(MaterialMeshInfo 등)를 자동으로 세팅해줍니다.
                var renderMeshArray = new RenderMeshArray(new[] { _terrainMaterial }, new[] { targetMesh });
                var renderMeshDescription = new RenderMeshDescription
                {
                    FilterSettings = RenderFilterSettings.Default,
                    LightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off
                };

                // 해당 엔티티에 렌더링에 필요한 모든 ECS 컴포넌트를 달아줍니다.
                RenderMeshUtility.AddComponents(
                    entity,
                    EntityManager,
                    renderMeshDescription,
                    renderMeshArray,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
                );

                // 5. 작업이 끝났으므로 업데이트 태그 제거
                ecb.RemoveComponent<MeshNeedsUpdateTag>(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        protected override void OnDestroy()
        {
            // 시스템 파괴 시(게임 종료/씬 전환) 모든 Mesh 메모리를 안전하게 해제합니다.
            foreach (var managedMesh in SystemAPI.Query<ChunkProceduralMesh>())
            {
                managedMesh.Dispose();
            }
        }
    }
}