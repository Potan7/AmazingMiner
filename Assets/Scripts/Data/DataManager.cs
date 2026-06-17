using System.Collections;
using UnityEngine;
using Potan.CoreUtils;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using CoreDriller.Map;

namespace CoreDriller
{
    public class DataManager : MonoSingleton<DataManager>
    {
        [Header("Materials")]
        public Material TerrainMaterial;

        Dictionary<int, ItemVisualData> ItemDataList;

        public ItemVisualData GetItemVisualData(int itemID)
        {
            if (ItemDataList != null && ItemDataList.TryGetValue(itemID, out var visualData))
            {
                return visualData;
            }
            else
            {
                Debug.LogWarning($"ItemID {itemID} not found in ItemDataList.");
                return null;
            }
        }

        private bool isInitialized = false;

        private BlobAssetReference<ItemDatabaseBlob> itemBlobRef;
        private BlobAssetReference<BlockDatabaseBlob> blockBlobRef;

        protected override void OnAwake()
        {
#if UNITY_EDITOR
            // Force Addressables to Use Asset Database (Fastest) play mode
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                if (settings.ActivePlayModeDataBuilderIndex != 0)
                {
                    Debug.Log($"[DataManager] Changing Addressables ActivePlayModeDataBuilderIndex from {settings.ActivePlayModeDataBuilderIndex} to 0 (Use Asset Database)");
                    settings.ActivePlayModeDataBuilderIndex = 0;
                    UnityEditor.EditorUtility.SetDirty(settings);
                    UnityEditor.AssetDatabase.SaveAssets();
                }
            }
#endif
        }

        protected override void OnDestroy()
        {
            if (itemBlobRef.IsCreated)
            {
                itemBlobRef.Dispose();
            }
            if (blockBlobRef.IsCreated)
            {
                blockBlobRef.Dispose();
            }
            base.OnDestroy();
        }

        public async UniTask InitializeAsync()
        {
            if (isInitialized) return;

            try
            {
                // Explicitly initialize Addressables first to avoid deadlock issues in standalone builds
                await Addressables.InitializeAsync().ToUniTask();

                // 1. Load ItemDataSO
                var itemDataList = await Addressables.LoadAssetsAsync<ItemDataSO>("ItemData", null).ToUniTask();

                if (itemDataList != null)
                {
                    ItemDataList = new Dictionary<int, ItemVisualData>();
                    List<ItemSpec> allItemSpecs = new List<ItemSpec>();

                    // Save visual data
                    foreach (var itemSO in itemDataList)
                    {
                        foreach (var itemVisualData in itemSO.ItemVisualDatas)
                        {
                            if (!ItemDataList.TryAdd(itemVisualData.ItemID, itemVisualData))
                            {
                                Debug.LogWarning($"Duplicate ItemID {itemVisualData.ItemID} found in {itemSO.name}.");
                            }
                        }
                        foreach (var itemSpec in itemSO.ItemSpecs)
                        {
                            allItemSpecs.Add(itemSpec);
                        }
                    }

                    BuildItemBlobAsset(allItemSpecs);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataManager] Failed to load ItemData from Addressables: {e}");
            }

            // 2. Load BlockDataSO
            try
            {
                var blockDataList = await Addressables.LoadAssetsAsync<BlockDataSO>("BlockData", null).ToUniTask();

                if (blockDataList != null)
                {
                    List<BlockSpec> allBlockSpecs = new List<BlockSpec>();
                    Dictionary<int, BlockVisualData> blockVisualDatas = new Dictionary<int, BlockVisualData>();

                    foreach (var blockSO in blockDataList)
                    {
                        foreach (var blockVisualData in blockSO.BlockVisualDatas)
                        {
                            if (!blockVisualDatas.TryAdd(blockVisualData.BlockType, blockVisualData))
                            {
                                Debug.LogWarning($"Duplicate BlockType {blockVisualData.BlockType} found in {blockSO.name}.");
                            }
                        }
                        foreach (var blockSpec in blockSO.BlockSpecs)
                        {
                            allBlockSpecs.Add(blockSpec);
                        }
                    }

                    BuildBlockBlobAsset(allBlockSpecs, blockVisualDatas);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataManager] Failed to load BlockData from Addressables: {e}");
            }

            isInitialized = true;
            Debug.Log("[DataManager] Asynchronous Initialization Completed.");
        }

        void BuildItemBlobAsset(List<ItemSpec> itemSpecs)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref ItemDatabaseBlob root = ref builder.ConstructRoot<ItemDatabaseBlob>();

                BlobBuilderArray<ItemBlobInfo> array = builder.Allocate(ref root.Items, itemSpecs.Count);

                for (int i = 0; i < itemSpecs.Count; i++)
                {
                    var spec = itemSpecs[i];
                    array[i] = new ItemBlobInfo
                    {
                        ItemID = spec.ItemID,
                        Value = spec.Value,
                        MaxStackMultiplier = spec.MaxStackMultiplier,
                    };
                }

                itemBlobRef = builder.CreateBlobAssetReference<ItemDatabaseBlob>(Allocator.Persistent);

                Entity ItemDBEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(ItemDBEntity, new ItemDatabaseReference { Reference = itemBlobRef });
            }
        }

        void BuildBlockBlobAsset(List<BlockSpec> blockSpecs, Dictionary<int, BlockVisualData> visualDatas)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref BlockDatabaseBlob root = ref builder.ConstructRoot<BlockDatabaseBlob>();
                BlobBuilderArray<BlockBlobInfo> array = builder.Allocate(ref root.Blocks, blockSpecs.Count);
                Texture2D terrainTexture = null;

                for (int i = 0; i < blockSpecs.Count; i++)
                {
                    var spec = blockSpecs[i];
                    float4[] uvRects = null;

                    if (visualDatas.TryGetValue(spec.BlockType, out var visual))
                    {
                        uvRects = CalculateBlockUVs(visual.TerrainSprites, visual.TerrainSprite);

                        // 텍스처 수집 (가장 먼저 나오는 유효한 스프라이트 텍스처 사용)
                        if (visual.TerrainSprites != null && visual.TerrainSprites.Count > 0)
                        {
                            foreach (var s in visual.TerrainSprites)
                            {
                                if (s != null && s.texture != null)
                                {
                                    terrainTexture = s.texture;
                                    break;
                                }
                            }
                        }
                        if (terrainTexture == null && visual.TerrainSprite != null && visual.TerrainSprite.texture != null)
                        {
                            terrainTexture = visual.TerrainSprite.texture;
                        }
                    }
                    else
                    {
                        uvRects = CalculateBlockUVs(null, null);
                    }

                    array[i] = new BlockBlobInfo
                    {
                        BlockType = spec.BlockType,
                        Hardness = spec.Hardness,
                        MiningTime = spec.MiningTime,
                        MaxHP = spec.MaxHP,
                        DropItemID = spec.DropItemID
                    };

                    BlobBuilderArray<float4> uvRectsBlob = builder.Allocate(ref array[i].UVRects, uvRects.Length);
                    for (int u = 0; u < uvRects.Length; u++)
                    {
                        uvRectsBlob[u] = uvRects[u];
                    }
                }

                // 동적으로 패킹된 아틀라스/스프라이트 시트 텍스처를 지형 머티리얼에 자동 매핑
                if (terrainTexture != null && TerrainMaterial != null)
                {
                    TerrainMaterial.mainTexture = terrainTexture;
                    if (TerrainMaterial.HasProperty("_BaseMap"))
                    {
                        TerrainMaterial.SetTexture("_BaseMap", terrainTexture);
                    }
                }

                blockBlobRef = builder.CreateBlobAssetReference<BlockDatabaseBlob>(Allocator.Persistent);

                Entity blockDBEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(blockDBEntity, new BlockDatabaseReference { Reference = blockBlobRef });
            }
        }

        public static float4 CalculateBlockUV(Sprite sprite)
        {
            if (sprite != null)
            {
                Texture2D texture = sprite.texture;
                if (texture != null)
                {
                    // sprite.textureRect 활용 (런타임 패킹 아틀라스 지원)
                    Rect texRect = sprite.textureRect;
                    float texWidth = texture.width;
                    float texHeight = texture.height;

                    return new float4(
                        texRect.width / texWidth,
                        texRect.height / texHeight,
                        texRect.x / texWidth,
                        texRect.y / texHeight
                    );
                }
            }

            // 스프라이트가 없을 때 디폴트 풀 텍스처 영역 반환
            return new float4(1f, 1f, 0f, 0f);
        }

        public static float4[] CalculateBlockUVs(List<Sprite> sprites, Sprite fallbackSprite)
        {
            if (sprites != null && sprites.Count > 0)
            {
                var rects = new List<float4>();
                for (int i = 0; i < sprites.Count; i++)
                {
                    if (sprites[i] != null)
                    {
                        rects.Add(CalculateBlockUV(sprites[i]));
                    }
                }
                if (rects.Count > 0)
                {
                    return rects.ToArray();
                }
            }

            if (fallbackSprite != null)
            {
                return new float4[] { CalculateBlockUV(fallbackSprite) };
            }

            return new float4[] { CalculateBlockUV(null) };
        }
    }
}