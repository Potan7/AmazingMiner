using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemDataSO))]
public class ItemDataSOEditor : Editor
{
    private SerializedProperty visualDatasProp;
    private SerializedProperty specsProp;
    private bool[] foldouts;
    private string filterString = "";
    private Vector2 scrollPosition;

    private void OnEnable()
    {
        visualDatasProp = serializedObject.FindProperty("ItemVisualDatas");
        specsProp = serializedObject.FindProperty("ItemSpecs");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 1. Sync array sizes
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
                for (int i = 0; i < count; i++) foldouts[i] = true; // Default to open
            }
        }

        // Title
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Unified Item Database Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        // Search bar
        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
        filterString = EditorGUILayout.TextField(filterString);
        if (GUILayout.Button("Clear", GUILayout.Width(50)))
        {
            filterString = "";
            GUI.FocusControl(null); // Unfocus search field
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Scroll view for the item list
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(600));

        int deleteIndex = -1;
        for (int i = 0; i < count; i++)
        {
            SerializedProperty visualElement = visualDatasProp.GetArrayElementAtIndex(i);
            SerializedProperty specElement = specsProp.GetArrayElementAtIndex(i);

            SerializedProperty idVisual = visualElement.FindPropertyRelative("ItemID");
            SerializedProperty nameProp = visualElement.FindPropertyRelative("Name");
            SerializedProperty descProp = visualElement.FindPropertyRelative("Description");
            SerializedProperty iconProp = visualElement.FindPropertyRelative("Icon");

            SerializedProperty idSpec = specElement.FindPropertyRelative("ItemID");
            SerializedProperty valueProp = specElement.FindPropertyRelative("Value");
            SerializedProperty maxStackProp = specElement.FindPropertyRelative("MaxStackMultiplier");

            // Sync IDs: they must be identical
            if (idSpec.intValue != idVisual.intValue)
            {
                idSpec.intValue = idVisual.intValue;
            }

            string itemName = nameProp.stringValue;
            if (string.IsNullOrEmpty(itemName)) itemName = $"Item {idVisual.intValue} (No Name)";

            // Filter checking
            if (!string.IsNullOrEmpty(filterString))
            {
                bool matchesName = itemName.IndexOf(filterString, System.StringComparison.OrdinalIgnoreCase) >= 0;
                bool matchesID = idVisual.intValue.ToString().Contains(filterString);
                if (!matchesName && !matchesID)
                    continue;
            }

            // Draw Item Panel
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            // Header Row
            EditorGUILayout.BeginHorizontal();
            
            // Re-orderable foldout style title
            foldouts[i] = EditorGUILayout.Foldout(foldouts[i], $"{itemName} [ID: {idVisual.intValue}]", true);
            
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
                
                // Left side: fields
                EditorGUILayout.BeginVertical();
                
                // ID Field (with sync)
                int newID = EditorGUILayout.IntField("Item ID", idVisual.intValue);
                if (newID != idVisual.intValue)
                {
                    idVisual.intValue = newID;
                    idSpec.intValue = newID;
                }

                EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));
                
                EditorGUILayout.LabelField("Description");
                descProp.stringValue = EditorGUILayout.TextArea(descProp.stringValue, GUILayout.Height(40));

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Specs (ECS / Logic)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(valueProp, new GUIContent("Value (Gold)"));
                EditorGUILayout.PropertyField(maxStackProp, new GUIContent("Max Stack Multiplier"));

                EditorGUILayout.EndVertical();

                // Right side: Icon Preview & Selector
                EditorGUILayout.BeginVertical(GUILayout.Width(90));
                
                Sprite sprite = (Sprite)iconProp.objectReferenceValue;
                Texture2D texture = null;
                if (sprite != null)
                {
                    texture = AssetPreview.GetAssetPreview(sprite);
                }
                
                // Draw Icon preview
                Rect rect = GUILayoutUtility.GetRect(70, 70, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                rect.x += 10; // Center offset a bit
                if (texture != null)
                {
                    GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
                }
                else
                {
                    GUI.Box(rect, "No Icon");
                }
                
                EditorGUILayout.Space(2);
                
                // Object picker below preview
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

        // Add Button
        if (GUILayout.Button("Add New Item", GUILayout.Height(30)))
        {
            // Add element to both arrays
            visualDatasProp.InsertArrayElementAtIndex(count);
            specsProp.InsertArrayElementAtIndex(count);

            // Initialize default values for the new element
            SerializedProperty newVisual = visualDatasProp.GetArrayElementAtIndex(count);
            SerializedProperty newSpec = specsProp.GetArrayElementAtIndex(count);

            // Find next available ID
            int nextID = 1;
            for (int i = 0; i < count; i++)
            {
                int id = visualDatasProp.GetArrayElementAtIndex(i).FindPropertyRelative("ItemID").intValue;
                if (id >= nextID) nextID = id + 1;
            }

            newVisual.FindPropertyRelative("ItemID").intValue = nextID;
            newVisual.FindPropertyRelative("Name").stringValue = "New Item";
            newVisual.FindPropertyRelative("Description").stringValue = "";
            newVisual.FindPropertyRelative("Icon").objectReferenceValue = null;

            newSpec.FindPropertyRelative("ItemID").intValue = nextID;
            newSpec.FindPropertyRelative("Value").intValue = 0;
            newSpec.FindPropertyRelative("MaxStackMultiplier").floatValue = 1.0f;
            
            // Expand the new item
            var tempFoldouts = foldouts;
            foldouts = new bool[count + 1];
            if (tempFoldouts != null) System.Array.Copy(tempFoldouts, foldouts, tempFoldouts.Length);
            foldouts[count] = true;
        }

        // Handle delete outside loop
        if (deleteIndex >= 0)
        {
            DeleteArrayElement(visualDatasProp, deleteIndex);
            DeleteArrayElement(specsProp, deleteIndex);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DeleteArrayElement(SerializedProperty listProp, int index)
    {
        int originalSize = listProp.arraySize;
        listProp.DeleteArrayElementAtIndex(index);
        if (listProp.arraySize == originalSize)
        {
            // If DeleteArrayElementAtIndex set reference to null, call it again to actually remove it
            listProp.DeleteArrayElementAtIndex(index);
        }
    }
}
