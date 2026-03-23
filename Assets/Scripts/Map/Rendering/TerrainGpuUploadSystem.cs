using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Rendering;
using Unity.Entities.Graphics;
using System;

namespace CoreDriller.Map.Rendering
{
    public class ChunkProceduralMesh : IComponentData, IDisposable
    {
        public Mesh GeneratedMesh;
        public void Dispose()
        {
            if (GeneratedMesh != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(GeneratedMesh);
                else UnityEngine.Object.DestroyImmediate(GeneratedMesh);
                GeneratedMesh = null;
            }
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TerrainGpuUploadSystem : SystemBase
    {
        private Material _terrainMaterial;
        private EntityQuery _updateQuery;

        protected override void OnCreate()
        {
            _updateQuery = GetEntityQuery(
                ComponentType.ReadOnly<MeshNeedsUpdateTag>(),
                ComponentType.ReadWrite<ChunkVertex>(),
                ComponentType.ReadWrite<ChunkTriangle>()
            );
            RequireForUpdate(_updateQuery);
            _terrainMaterial = Resources.Load<Material>("TerrainMaterial");
        }

        protected override void OnUpdate()
        {
            // 1. 엔티티 배열 추출 (ToNativeArray 대신 복사본 사용으로 안전성 확보)
            using var entities = _updateQuery.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];

                Mesh targetMesh;
                bool isNewMesh = !EntityManager.HasComponent<ChunkProceduralMesh>(entity);

                // 2. 구조적 변경(컴포넌트 추가)을 먼저 수행
                if (isNewMesh)
                {
                    targetMesh = new Mesh();
                    targetMesh.MarkDynamic();
                    EntityManager.AddComponentData(entity, new ChunkProceduralMesh { GeneratedMesh = targetMesh });

                    var renderMeshArray = new RenderMeshArray(new[] { _terrainMaterial }, new[] { targetMesh });
                    var renderMeshDescription = new RenderMeshDescription
                    {
                        FilterSettings = RenderFilterSettings.Default,
                        LightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off
                    };

                    RenderMeshUtility.AddComponents(
                        entity,
                        EntityManager,
                        renderMeshDescription,
                        renderMeshArray,
                        MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
                    );
                }
                else
                {
                    targetMesh = EntityManager.GetComponentData<ChunkProceduralMesh>(entity).GeneratedMesh;
                }

                // 3. 🚨 중요: 구조적 변경이 끝난 후 '새로' 버퍼를 가져옴 (핸들 무효화 방지)
                var vertices = EntityManager.GetBuffer<ChunkVertex>(entity);
                var triangles = EntityManager.GetBuffer<ChunkTriangle>(entity);

                if (vertices.Length > 0 && triangles.Length > 0)
                {
                    targetMesh.Clear();
                    targetMesh.SetVertices(vertices.Reinterpret<Vector3>().AsNativeArray());
                    targetMesh.SetIndices(triangles.Reinterpret<int>().AsNativeArray(), MeshTopology.Triangles, 0);
                    targetMesh.RecalculateNormals();
                    targetMesh.RecalculateBounds();

                    // ECS 엔티티의 가시성 컬링 크기도 실제 메쉬 크기에 맞춰 갱신
                    EntityManager.SetComponentData(entity, new RenderBounds { Value = targetMesh.bounds.ToAABB() });
                }

                // 4. 태그 제거는 ECB에 담아 루프가 끝난 후 일괄 처리 (다음 루프의 핸들 보호)
                ecb.RemoveComponent<MeshNeedsUpdateTag>(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        protected override void OnDestroy()
        {
            var query = EntityManager.CreateEntityQuery(typeof(ChunkProceduralMesh));
            if (!query.IsEmpty)
            {
                var entities = query.ToEntityArray(Allocator.Temp);
                foreach (var entity in entities)
                {
                    var meshData = EntityManager.GetComponentData<ChunkProceduralMesh>(entity);
                    meshData.Dispose();
                }
                entities.Dispose();
            }
        }
    }
}
