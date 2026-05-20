using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SampleSceneMinionManagerSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RequestPath = "Temp/SetupSampleSceneMinionManager.request";

    [InitializeOnLoadMethod]
    private static void AutoSetupWhenRequested()
    {
        if (!File.Exists(RequestPath))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(RequestPath))
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update += SetupWhenEditorLeavesPlayMode;
                return;
            }

            File.Delete(RequestPath);
            SetupSampleScene();
        };
    }

    private static void SetupWhenEditorLeavesPlayMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EditorApplication.update -= SetupWhenEditorLeavesPlayMode;
        if (File.Exists(RequestPath))
        {
            File.Delete(RequestPath);
            SetupSampleScene();
        }
    }

    [MenuItem("Tools/Minions/Setup Sample Scene Minion Manager")]
    public static void SetupSampleScene()
    {
        EditorSceneManager.OpenScene(ScenePath);

        MinionManager manager = Object.FindObjectOfType<MinionManager>();
        if (manager == null)
        {
            GameObject managerObject = new GameObject("Minion Manager");
            manager = managerObject.AddComponent<MinionManager>();
        }
        else
        {
            manager.gameObject.name = "Minion Manager";
        }

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Selection.activeObject = manager.gameObject;
        Debug.Log("SampleScene Minion Manager is ready for visual prefab assignment.");
    }
}
