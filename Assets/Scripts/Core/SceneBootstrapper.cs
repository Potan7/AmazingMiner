using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

namespace CoreDriller.Core
{
    public class SceneBootstrapper : MonoBehaviour
    {
        private const string OriginalScenePrefKey = "EditorPlayModeOriginalScene";
        [SerializeField] private string defaultNextScene = "Assets/Scenes/MainScene.unity";

        private async void Start()
        {
            // Wait for DataManager to complete its asynchronous Addressables initialization
            if (DataManager.Instance != null)
            {
                Debug.Log("[SceneBootstrapper] Awaiting DataManager initialization...");
                await DataManager.Instance.InitializeAsync();
            }

#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.HasKey(OriginalScenePrefKey))
            {
                string originalScene = UnityEditor.EditorPrefs.GetString(OriginalScenePrefKey);
                UnityEditor.EditorPrefs.DeleteKey(OriginalScenePrefKey);

                if (!string.IsNullOrEmpty(originalScene) && originalScene != SceneManager.GetActiveScene().path)
                {
                    Debug.Log($"[SceneBootstrapper] Restoring original scene: {originalScene}");
                    SceneManager.LoadScene(originalScene);
                    return;
                }
            }
#endif
            // If not in Editor or no original scene was saved, load the default next scene
            Debug.Log($"[SceneBootstrapper] Loading default next scene: {defaultNextScene}");
            SceneManager.LoadScene(defaultNextScene);
        }
    }
}
