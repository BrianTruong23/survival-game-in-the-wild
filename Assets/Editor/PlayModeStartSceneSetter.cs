using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class PlayModeStartSceneSetter
{
    private const string StartScenePath = "Assets/Scenes/StartScene.unity";

    static PlayModeStartSceneSetter()
    {
        SceneAsset startScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(StartScenePath);
        if (startScene != null && EditorSceneManager.playModeStartScene != startScene)
        {
            EditorSceneManager.playModeStartScene = startScene;
        }
    }
}
