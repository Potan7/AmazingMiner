using Unity.Entities;
using Unity.Rendering;
using Unity.Entities.Graphics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace CoreDriller.Map.Rendering
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct DebrisMaterialInitializeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MapConfigData>();
            state.RequireForUpdate<ItemDatabaseReference>(); // 데이터베이스 로드 완료 대기
        }

        public void OnUpdate(ref SystemState state)
        {
            // 단 한 번만 실행하기 위해 업데이트 비활성화
            state.Enabled = false;

            if (DataManager.Instance == null)
            {
                Debug.LogWarning("[DebrisMaterialInitializeSystem] DataManager 인스턴스를 찾을 수 없어 런타임 파편 프리팹 자동 생성을 취소합니다.");
                return;
            }

            var configEntity = SystemAPI.GetSingletonEntity<MapConfigData>();
            var config = SystemAPI.GetSingleton<MapConfigData>();

            // DOTS Instancing과 _UVRect 연산을 지원하는 원본 SpriteMaterial 가져오기
            if (!state.EntityManager.HasComponent<MapConfigMaterials>(configEntity))
            {
                Debug.LogWarning("[DebrisMaterialInitializeSystem] MapConfigMaterials Managed 컴포넌트가 없어 런타임 자동 생성을 중단합니다.");
                return;
            }
            var materials = state.EntityManager.GetComponentObject<MapConfigMaterials>(configEntity);
            Material baseMaterial = materials.SpriteMaterial != null ? materials.SpriteMaterial : materials.TerrainMaterial;
            if (baseMaterial == null)
            {
                Debug.LogWarning("[DebrisMaterialInitializeSystem] 복제하여 사용할 원본 머티리얼이 지정되어 있지 않습니다.");
                return;
            }

            // 공용 DebrisPrefab에서 원본 메쉬 추출
            if (!state.EntityManager.HasComponent<RenderMeshArray>(config.DebrisPrefab))
            {
                Debug.LogWarning("[DebrisMaterialInitializeSystem] 공용 debrisPrefab에 RenderMeshArray가 없어 런타임 자동 생성을 중단합니다.");
                return;
            }
            var originRenderMeshArray = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(config.DebrisPrefab);
            if (originRenderMeshArray.Meshes.Length == 0)
            {
                Debug.LogWarning("[DebrisMaterialInitializeSystem] 공용 debrisPrefab에 등록된 메쉬가 없습니다.");
                return;
            }
            var prefabMesh = originRenderMeshArray.Meshes[0];

            // SafetyHandle 무효화(Disposed) 에러를 방지하기 위해 ECB 사용 (Error 2 해결)
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 매핑 버퍼 초기화
            if (!state.EntityManager.HasComponent<ItemDebrisPrefabElement>(configEntity))
            {
                ecb.AddBuffer<ItemDebrisPrefabElement>(configEntity);
            }
            else
            {
                // 기존 버퍼가 이미 생성되어 있는 경우 클리어
                var existingBuffer = state.EntityManager.GetBuffer<ItemDebrisPrefabElement>(configEntity);
                existingBuffer.Clear();
            }

            // 아이템 데이터베이스 목록에서 기획 데이터를 순회하며 프리팹 자동 생성
            ref var itemSpecs = ref SystemAPI.GetSingleton<ItemDatabaseReference>().Reference.Value.Items;

            for (int i = 0; i < itemSpecs.Length; i++)
            {
                int itemID = itemSpecs[i].ItemID;
                var visualData = DataManager.Instance.GetItemVisualData(itemID);
                if (visualData == null || visualData.Icon == null)
                    continue;

                Sprite sprite = visualData.Icon;
                Texture2D texture = sprite.texture;

                if (texture == null)
                    continue;

                // SpriteMaterial(SpriteShader.shadergraph)을 복제하여 텍스처를 할당
                Material customMaterial = new Material(baseMaterial);
                customMaterial.mainTexture = texture;

                // 원본 DebrisPrefab 복제 생성
                var customDebrisEntity = state.EntityManager.Instantiate(config.DebrisPrefab);

                // RenderMesh 컴포넌트 재조립
                var renderMeshArray = new RenderMeshArray(
                    new[] { customMaterial },
                    new[] { prefabMesh }
                );

                var desc = new RenderMeshDescription
                {
                    FilterSettings = RenderFilterSettings.Default,
                    LightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off
                };

                // RenderMesh 컴포넌트 교체 등록 (AddComponents는 즉시 반영)
                RenderMeshUtility.AddComponents(
                    customDebrisEntity,
                    state.EntityManager,
                    desc,
                    renderMeshArray,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
                );

                // 스프라이트 아틀라스 패킹 상태 등을 고려해 UVRect 자동 연산
                // sprite.rect 대신 실제 런타임 패킹 텍스처 상의 좌표인 textureRect를 사용해야 정상적으로 컷아웃됩니다.
                Rect texRect = sprite.textureRect;
                float texWidth = texture.width;
                float texHeight = texture.height;

                float scaleX = texRect.width / texWidth;
                float scaleY = texRect.height / texHeight;
                float offsetX = texRect.x / texWidth;
                float offsetY = texRect.y / texHeight;

                // 생성된 전용 프리팹에 완성된 UVRect 설정
                if (state.EntityManager.HasComponent<UVRect>(customDebrisEntity))
                {
                    state.EntityManager.SetComponentData(customDebrisEntity, new UVRect
                    {
                        Value = new float4(scaleX, scaleY, offsetX, offsetY)
                    });
                }
                else
                {
                    state.EntityManager.AddComponentData(customDebrisEntity, new UVRect
                    {
                        Value = new float4(scaleX, scaleY, offsetX, offsetY)
                    });
                }

                // 버퍼 추가 커맨드를 ECB에 예약합니다. (루프 도중의 SafetyHandle 무효화 방지)
                ecb.AppendToBuffer(configEntity, new ItemDebrisPrefabElement
                {
                    ItemID = itemID,
                    DebrisPrefab = customDebrisEntity
                });

                Debug.Log($"[DebrisMaterialInitializeSystem] Automatically generated custom debris prefab for ItemID: {itemID} using Sprite '{sprite.name}'");
            }

            // 예약된 커맨드 버퍼 실행 및 메모리 정리
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
