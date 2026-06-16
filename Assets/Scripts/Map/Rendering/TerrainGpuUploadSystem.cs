using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Rendering;
using Unity.Entities.Graphics;
using System;
using UnityEngine.Rendering;

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
        private EntityQuery _updateQuery;

        protected override void OnCreate()
        {
            // UnityEngine.Debug.Log("[TerrainGpuUploadSystem] OnCreate called!");
            _updateQuery = GetEntityQuery(
                ComponentType.ReadOnly<MeshNeedsUpdateTag>(),
                ComponentType.ReadWrite<ChunkVertex>(),
                ComponentType.ReadWrite<ChunkTriangle>()
            );
            // RequireForUpdate(_updateQuery);
        }

        protected override void OnUpdate()
        {
            if (DataManager.Instance == null)
            {
                UnityEngine.Debug.Log("[TerrainGpuUploadSystem] OnUpdate: DataManager.Instance is NULL!");
                return;
            }
            if (DataManager.Instance.TerrainMaterial == null)
            {
                UnityEngine.Debug.Log("[TerrainGpuUploadSystem] OnUpdate: DataManager.Instance.TerrainMaterial is NULL!");
                return;
            }

            var terrainMaterial = DataManager.Instance.TerrainMaterial;

            // 1. 엔티티 배열 추출 (ToNativeArray 대신 복사본 사용으로 안전성 확보)
            using var entities = _updateQuery.ToEntityArray(Allocator.Temp);
            // if (entities.Length > 0)
            // {
            //     UnityEngine.Debug.Log($"[TerrainGpuUploadSystem] OnUpdate: Found {entities.Length} chunks needing GPU upload.");
            // }

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

                    var renderMeshArray = new RenderMeshArray(new[] { terrainMaterial }, new[] { targetMesh });
                    var renderMeshDescription = new RenderMeshDescription
                    {
                        FilterSettings = RenderFilterSettings.Default,
                        LightProbeUsage = LightProbeUsage.Off
                    };

                    RenderMeshUtility.AddComponents(
                        entity,
                        EntityManager,
                        renderMeshDescription,
                        renderMeshArray,
                        MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
                    );

                    // SpriteShader.shadergraph의 Hybrid Instanced 속성인 _UVRect에 바인딩할 초기값 설정 (1:1 풀 스케일, 오프셋 없음)
                    EntityManager.AddComponentData(entity, new UVRect { Value = new float4(1f, 1f, 0f, 0f) });
                }
                else
                {
                    targetMesh = EntityManager.GetComponentData<ChunkProceduralMesh>(entity).GeneratedMesh;
                }

                // 3. 중요: 구조적 변경이 끝난 후 '새로' 버퍼를 가져옴 (핸들 무효화 방지)
                // 또한, 이전 Job(TerrainMeshBuilderSystem)이 완료될 때까지 기다려야 합니다.
                this.Dependency.Complete(); 
                
                var vertices = EntityManager.GetBuffer<ChunkVertex>(entity);
                var triangles = EntityManager.GetBuffer<ChunkTriangle>(entity);

                // 4번 로그 복구: 청크 업로드 정보 출력 (필요 시 주석 해제)
                // UnityEngine.Debug.Log($"[TerrainGpuUploadSystem] Chunk Entity: {entity}, Vertices: {vertices.Length}, Triangles: {triangles.Length}");

                // 빈 청크가 되더라도 메쉬를 Clear하여 그래픽을 지워야 합니다.
                targetMesh.Clear();

                if (vertices.Length > 0 && triangles.Length > 0)
                {
                    // 정점 레이아웃 설정 (Position: float3, TexCoord0: float2)
                    var layout = new[]
                    {
                        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2)
                    };

                    targetMesh.SetVertexBufferParams(vertices.Length, layout);
                    targetMesh.SetVertexBufferData(vertices.AsNativeArray(), 0, 0, vertices.Length);

                    targetMesh.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
                    targetMesh.SetIndexBufferData(triangles.AsNativeArray(), 0, 0, triangles.Length);

                    targetMesh.subMeshCount = 1;
                    targetMesh.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length));

                    targetMesh.RecalculateNormals();
                    targetMesh.RecalculateBounds();

                    // ECS 엔티티의 가시성 컬링 크기도 실제 메쉬 크기에 맞춰 갱신
                    EntityManager.SetComponentData(entity, new RenderBounds { Value = targetMesh.bounds.ToAABB() });
                }
                else
                {
                    // 빈 청크일 경우 Bounds를 0으로 초기화
                    EntityManager.SetComponentData(entity, new RenderBounds { Value = new AABB { Center = float3.zero, Extents = float3.zero } });
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
