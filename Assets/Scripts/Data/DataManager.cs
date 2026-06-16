using System.Collections;
using UnityEngine;
using Potan.CoreUtils;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
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

                BlobAssetReference<ItemDatabaseBlob> blobRef = builder.CreateBlobAssetReference<ItemDatabaseBlob>(Allocator.Persistent);

                Entity ItemDBEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(ItemDBEntity, new ItemDatabaseReference { Reference = blobRef });
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

                for (int i = 0; i < blockSpecs.Count; i++)
                {
                    var spec = blockSpecs[i];
                    int atlasIndex = 0;
                    if (visualDatas.TryGetValue(spec.BlockType, out var visual))
                    {
                        atlasIndex = visual.AtlasIndex;
                    }

                    array[i] = new BlockBlobInfo
                    {
                        BlockType = spec.BlockType,
                        Hardness = spec.Hardness,
                        MiningTime = spec.MiningTime,
                        MaxHP = spec.MaxHP,
                        AtlasIndex = atlasIndex,
                        DropItemID = spec.DropItemID
                    };
                }

                BlobAssetReference<BlockDatabaseBlob> blobRef = builder.CreateBlobAssetReference<BlockDatabaseBlob>(Allocator.Persistent);

                Entity blockDBEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(blockDBEntity, new BlockDatabaseReference { Reference = blobRef });
            }
        }
    }
}