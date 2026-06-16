using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CoreDriller.Editor
{
    [InitializeOnLoad]
    public static class PlayModeSceneBootstrapper
    {
        private const string BootstrapScenePath = "Assets/Scenes/BootScene.unity";
        private const string OriginalScenePrefKey = "EditorPlayModeOriginalScene";

        static PlayModeSceneBootstrapper()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.path != BootstrapScenePath && !string.IsNullOrEmpty(activeScene.path))
                {
                    // Save original scene to EditorPrefs
                    EditorPrefs.SetString(OriginalScenePrefKey, activeScene.path);
                    
                    // Force start scene to BootScene.unity
                    var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
                    if (sceneAsset != null)
                    {
                        EditorSceneManager.playModeStartScene = sceneAsset;
                        Debug.Log($"[PlayModeSceneBootstrapper] Redirecting play mode to: {BootstrapScenePath}");
                    }
                }
                else
                {
                    // Clear playModeStartScene if we are playing BootScene directly
                    EditorSceneManager.playModeStartScene = null;
                    EditorPrefs.DeleteKey(OriginalScenePrefKey);
                }
            }
        }
    }
}
