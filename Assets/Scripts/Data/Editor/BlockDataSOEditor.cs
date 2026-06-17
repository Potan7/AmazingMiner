using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BlockDataSO))]
public class BlockDataSOEditor : Editor
{
    private SerializedProperty visualDatasProp;
    private SerializedProperty specsProp;
    private bool[] foldouts;
    private string filterString = "";
    private Vector2 scrollPosition;

    private void OnEnable()
    {
        visualDatasProp = serializedObject.FindProperty("BlockVisualDatas");
        specsProp = serializedObject.FindProperty("BlockSpecs");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        int count = Mathf.Max(visualDatasProp.arraySize, specsProp.arraySize);
        if (visualDatasProp.arraySize != count) visualDatasProp.arraySize = count;
        if (specsProp.arraySize != count) specsProp.arraySize = count;

        if (foldouts == null || foldouts.Length != count)
        {
            var oldFoldouts = foldouts;
            foldouts = new bool[count];
            if (oldFoldouts != null)
            {
                for (int i = 0; i < Mathf.Min(oldFoldouts.Length, count); i++)
                {
                    foldouts[i] = oldFoldouts[i];
                }
            }
            else
            {
                for (int i = 0; i < count; i++) foldouts[i] = true;
            }
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Unified Block Database Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        // Search bar
        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
        filterString = EditorGUILayout.TextField(filterString);
        if (GUILayout.Button("Clear", GUILayout.Width(50)))
        {
            filterString = "";
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(600));

        int deleteIndex = -1;
        for (int i = 0; i < count; i++)
        {
            SerializedProperty visualElement = visualDatasProp.GetArrayElementAtIndex(i);
            SerializedProperty specElement = specsProp.GetArrayElementAtIndex(i);

            SerializedProperty idVisual = visualElement.FindPropertyRelative("BlockType");
            SerializedProperty nameProp = visualElement.FindPropertyRelative("Name");
            SerializedProperty descProp = visualElement.FindPropertyRelative("Description");
            SerializedProperty iconProp = visualElement.FindPropertyRelative("Icon");
            SerializedProperty terrainSpriteProp = visualElement.FindPropertyRelative("TerrainSprite");
            SerializedProperty atlasIdxProp = visualElement.FindPropertyRelative("AtlasIndex");

            SerializedProperty idSpec = specElement.FindPropertyRelative("BlockType");
            SerializedProperty hardnessProp = specElement.FindPropertyRelative("Hardness");
            SerializedProperty miningTimeProp = specElement.FindPropertyRelative("MiningTime");
            SerializedProperty maxHPProp = specElement.FindPropertyRelative("MaxHP");

            SerializedProperty dropItemIDVisual = visualElement.FindPropertyRelative("DropItemID");
            SerializedProperty dropItemIDSpec = specElement.FindPropertyRelative("DropItemID");

            // Sync BlockType IDs
            if (idSpec.intValue != idVisual.intValue)
            {
                idSpec.intValue = idVisual.intValue;
            }

            // Sync DropItem IDs
            if (dropItemIDSpec.intValue != dropItemIDVisual.intValue)
            {
                dropItemIDSpec.intValue = dropItemIDVisual.intValue;
            }

            string blockName = nameProp.stringValue;
            if (string.IsNullOrEmpty(blockName)) blockName = $"Block {idVisual.intValue} (No Name)";

            if (!string.IsNullOrEmpty(filterString))
            {
                bool matchesName = blockName.IndexOf(filterString, System.StringComparison.OrdinalIgnoreCase) >= 0;
                bool matchesID = idVisual.intValue.ToString().Contains(filterString);
                if (!matchesName && !matchesID)
                    continue;
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            EditorGUILayout.BeginHorizontal();
            foldouts[i] = EditorGUILayout.Foldout(foldouts[i], $"{blockName} [ID: {idVisual.intValue}]", true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                deleteIndex = i;
            }
            EditorGUILayout.EndHorizontal();

            if (foldouts[i])
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(4);

                EditorGUILayout.BeginHorizontal();
                
                // Left side fields
                EditorGUILayout.BeginVertical();
                
                int newID = EditorGUILayout.IntField("Block Type ID", idVisual.intValue);
                if (newID != idVisual.intValue)
                {
                    if (dropItemIDVisual.intValue == idVisual.intValue)
                    {
                        dropItemIDVisual.intValue = newID;
                        dropItemIDSpec.intValue = newID;
                    }
                    idVisual.intValue = newID;
                    idSpec.intValue = newID;
                }

                int newDropItemID = EditorGUILayout.IntField("Drop Item ID", dropItemIDVisual.intValue);
                if (newDropItemID != dropItemIDVisual.intValue)
                {
                    dropItemIDVisual.intValue = newDropItemID;
                    dropItemIDSpec.intValue = newDropItemID;
                }

                EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));
                
                EditorGUILayout.LabelField("Description");
                descProp.stringValue = EditorGUILayout.TextArea(descProp.stringValue, GUILayout.Height(40));

                EditorGUILayout.PropertyField(terrainSpriteProp, new GUIContent("Terrain Sprite"));
                EditorGUILayout.PropertyField(atlasIdxProp, new GUIContent("Atlas Index (Fallback)"));

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Specs (Logic)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(hardnessProp, new GUIContent("Hardness"));
                EditorGUILayout.PropertyField(miningTimeProp, new GUIContent("Mining Time (sec)"));
                EditorGUILayout.PropertyField(maxHPProp, new GUIContent("Max HP"));

                EditorGUILayout.EndVertical();

                // Right side preview
                EditorGUILayout.BeginVertical(GUILayout.Width(90));
                
                Sprite sprite = (Sprite)iconProp.objectReferenceValue;
                Texture2D texture = null;
                if (sprite != null)
                {
                    texture = AssetPreview.GetAssetPreview(sprite);
                }
                
                Rect rect = GUILayoutUtility.GetRect(70, 70, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                rect.x += 10;
                if (texture != null)
                {
                    GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
                }
                else
                {
                    GUI.Box(rect, "No Icon");
                }
                
                EditorGUILayout.Space(2);
                
                Rect objectFieldRect = GUILayoutUtility.GetRect(85, 18, GUILayout.ExpandWidth(false));
                objectFieldRect.x += 2;
                iconProp.objectReferenceValue = EditorGUI.ObjectField(objectFieldRect, iconProp.objectReferenceValue, typeof(Sprite), false);

                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Add New Block", GUILayout.Height(30)))
        {
            visualDatasProp.InsertArrayElementAtIndex(count);
            specsProp.InsertArrayElementAtIndex(count);

            SerializedProperty newVisual = visualDatasProp.GetArrayElementAtIndex(count);
            SerializedProperty newSpec = specsProp.GetArrayElementAtIndex(count);

            int nextID = 1;
            for (int i = 0; i < count; i++)
            {
                int id = visualDatasProp.GetArrayElementAtIndex(i).FindPropertyRelative("BlockType").intValue;
                if (id >= nextID) nextID = id + 1;
            }

            newVisual.FindPropertyRelative("BlockType").intValue = nextID;
            newVisual.FindPropertyRelative("DropItemID").intValue = nextID;
            newVisual.FindPropertyRelative("Name").stringValue = "New Block";
            newVisual.FindPropertyRelative("Description").stringValue = "";
            newVisual.FindPropertyRelative("Icon").objectReferenceValue = null;
            newVisual.FindPropertyRelative("TerrainSprite").objectReferenceValue = null;
            newVisual.FindPropertyRelative("AtlasIndex").intValue = 0;

            newSpec.FindPropertyRelative("BlockType").intValue = nextID;
            newSpec.FindPropertyRelative("DropItemID").intValue = nextID;
            newSpec.FindPropertyRelative("Hardness").floatValue = 1.0f;
            newSpec.FindPropertyRelative("MiningTime").floatValue = 1.0f;
            newSpec.FindPropertyRelative("MaxHP").floatValue = 1.0f;
            
            var tempFoldouts = foldouts;
            foldouts = new bool[count + 1];
            if (tempFoldouts != null) System.Array.Copy(tempFoldouts, foldouts, tempFoldouts.Length);
            foldouts[count] = true;
        }

        if (deleteIndex >= 0)
        {
            DeleteArrayElement(visualDatasProp, deleteIndex);
            DeleteArrayElement(specsProp, deleteIndex);
        }
        EditorGUILayout.Space(10);
        if (GUILayout.Button("Sync Mineable Blocks to Item Database (ItemDataSO)", GUILayout.Height(35)))
        {
            SyncBlocksToItemDatabase();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void SyncBlocksToItemDatabase()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemDataSO");
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Sync Failed", "No ItemDataSO asset found in the project. Please create one first.", "OK");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        ItemDataSO itemData = AssetDatabase.LoadAssetAtPath<ItemDataSO>(path);
        if (itemData == null)
        {
            EditorUtility.DisplayDialog("Sync Failed", $"Failed to load ItemDataSO at {path}.", "OK");
            return;
        }

        SerializedObject itemSerializedObject = new SerializedObject(itemData);
        SerializedProperty itemVisuals = itemSerializedObject.FindProperty("ItemVisualDatas");
        SerializedProperty itemSpecs = itemSerializedObject.FindProperty("ItemSpecs");

        int syncedCount = 0;
        int updatedCount = 0;

        int blockCount = Mathf.Min(visualDatasProp.arraySize, specsProp.arraySize);

        for (int i = 0; i < blockCount; i++)
        {
            SerializedProperty blockVisual = visualDatasProp.GetArrayElementAtIndex(i);
            SerializedProperty blockSpec = specsProp.GetArrayElementAtIndex(i);

            int dropItemID = blockVisual.FindPropertyRelative("DropItemID").intValue;
            if (dropItemID == 0) continue; // Skip blocks with no drop

            string blockName = blockVisual.FindPropertyRelative("Name").stringValue;
            string blockDesc = blockVisual.FindPropertyRelative("Description").stringValue;
            Sprite blockIcon = blockVisual.FindPropertyRelative("Icon").objectReferenceValue as Sprite;

            // Find matching item in ItemDataSO
            int itemIndex = -1;
            int itemSize = Mathf.Max(itemVisuals.arraySize, itemSpecs.arraySize);
            if (itemVisuals.arraySize != itemSize) itemVisuals.arraySize = itemSize;
            if (itemSpecs.arraySize != itemSize) itemSpecs.arraySize = itemSize;

            for (int j = 0; j < itemSize; j++)
            {
                int itemID = itemVisuals.GetArrayElementAtIndex(j).FindPropertyRelative("ItemID").intValue;
                if (itemID == dropItemID)
                {
                    itemIndex = j;
                    break;
                }
            }

            if (itemIndex == -1)
            {
                // Add new item
                int newIndex = itemSize;
                itemVisuals.InsertArrayElementAtIndex(newIndex);
                itemSpecs.InsertArrayElementAtIndex(newIndex);

                SerializedProperty newItemVisual = itemVisuals.GetArrayElementAtIndex(newIndex);
                SerializedProperty newItemSpec = itemSpecs.GetArrayElementAtIndex(newIndex);

                newItemVisual.FindPropertyRelative("ItemID").intValue = dropItemID;
                newItemVisual.FindPropertyRelative("Name").stringValue = blockName;
                newItemVisual.FindPropertyRelative("Description").stringValue = blockDesc;
                newItemVisual.FindPropertyRelative("Icon").objectReferenceValue = blockIcon;

                newItemSpec.FindPropertyRelative("ItemID").intValue = dropItemID;
                newItemSpec.FindPropertyRelative("Value").intValue = 0; // Default value
                newItemSpec.FindPropertyRelative("MaxStackMultiplier").floatValue = 1.0f; // Default max stack

                syncedCount++;
            }
            else
            {
                // Update existing item's name/desc/icon if they differ
                SerializedProperty existingVisual = itemVisuals.GetArrayElementAtIndex(itemIndex);
                bool changed = false;

                if (existingVisual.FindPropertyRelative("Name").stringValue != blockName)
                {
                    existingVisual.FindPropertyRelative("Name").stringValue = blockName;
                    changed = true;
                }
                if (existingVisual.FindPropertyRelative("Description").stringValue != blockDesc)
                {
                    existingVisual.FindPropertyRelative("Description").stringValue = blockDesc;
                    changed = true;
                }
                if (existingVisual.FindPropertyRelative("Icon").objectReferenceValue != blockIcon)
                {
                    existingVisual.FindPropertyRelative("Icon").objectReferenceValue = blockIcon;
                    changed = true;
                }

                if (changed)
                {
                    updatedCount++;
                }
            }
        }

        if (syncedCount > 0 || updatedCount > 0)
        {
            itemSerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(itemData);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Sync Complete", $"Successfully synced to ItemDataSO:\n- Created: {syncedCount} items\n- Updated: {updatedCount} items", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Sync Complete", "No changes detected. All blocks are already in sync with ItemDataSO.", "OK");
        }
    }

    private void DeleteArrayElement(SerializedProperty listProp, int index)
    {
        int originalSize = listProp.arraySize;
        listProp.DeleteArrayElementAtIndex(index);
        if (listProp.arraySize == originalSize)
        {
            listProp.DeleteArrayElementAtIndex(index);
        }
    }
}
