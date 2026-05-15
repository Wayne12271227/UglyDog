using System.IO;
using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class UglyDogRoomLobbySceneBuilder
{
    private const string LobbyScenePath = "Assets/Scenes/RoomLobby.unity";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string GameScenePath = "Assets/Scenes/SampleScene.unity";
    private const string DogPrefabPath = "Assets/prefab/DOG.prefab";
    private const string CatPrefabPath = "Assets/prefab/CAT.prefab";
    private const string PendingBuildRequestPath = "Temp/BuildRoomLobby.request";

    [InitializeOnLoadMethod]
    private static void RunPendingBuildRequest()
    {
        if (!File.Exists(PendingBuildRequestPath))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(PendingBuildRequestPath))
            {
                return;
            }

            File.Delete(PendingBuildRequestPath);
            BuildRoomLobbyScene();
        };
    }

    [MenuItem("UglyDog/Photon/Build Room Lobby Scene")]
    public static void BuildRoomLobbyScene()
    {
        ConfigureNetworkPrefab(DogPrefabPath);
        ConfigureNetworkPrefab(CatPrefabPath);
        CreateLobbyScene();
        ConfigureMainMenuTarget();
        EnsureBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("RoomLobby scene is ready. MainMenu Start now opens the room lobby.");
    }

    private static void ConfigureNetworkPrefab(string prefabPath)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (prefabRoot.GetComponent<NetworkObject>() == null)
            {
                prefabRoot.AddComponent<NetworkObject>();
            }

            if (prefabRoot.GetComponent<NetworkTransform>() == null)
            {
                prefabRoot.AddComponent<NetworkTransform>();
            }

            if (prefabRoot.GetComponent<UglyDogNetworkPlayer>() == null)
            {
                prefabRoot.AddComponent<UglyDogNetworkPlayer>();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        Object prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        string[] labels = AssetDatabase.GetLabels(prefab);
        if (!labels.Contains("FusionPrefab"))
        {
            AssetDatabase.SetLabels(prefab, labels.Concat(new[] { "FusionPrefab" }).ToArray());
        }

        Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
    }

    private static void CreateLobbyScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "RoomLobby";

        GameObject lobbyObject = new GameObject("Room Lobby");
        UglyDogRoomLobby lobby = lobbyObject.AddComponent<UglyDogRoomLobby>();

        NetworkObject dogNetworkObject = AssetDatabase.LoadAssetAtPath<GameObject>(DogPrefabPath).GetComponent<NetworkObject>();
        NetworkObject catNetworkObject = AssetDatabase.LoadAssetAtPath<GameObject>(CatPrefabPath).GetComponent<NetworkObject>();
        SerializedObject serializedLobby = new SerializedObject(lobby);
        serializedLobby.FindProperty("playerPrefab").objectReferenceValue = dogNetworkObject;
        serializedLobby.FindProperty("secondPlayerPrefab").objectReferenceValue = catNetworkObject;
        serializedLobby.FindProperty("gameSceneName").stringValue = Path.GetFileNameWithoutExtension(GameScenePath);
        serializedLobby.ApplyModifiedPropertiesWithoutUndo();

        lobby.CreateEditorPreviewUi();
        EditorSceneManager.SaveScene(scene, LobbyScenePath);
    }

    private static void ConfigureMainMenuTarget()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        bool restoreActiveScene = activeScene.IsValid() && activeScene.path != MainMenuScenePath;

        Scene mainMenuScene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        MainMenuController controller = Object.FindObjectOfType<MainMenuController>();
        if (controller != null)
        {
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("gameSceneName").stringValue = Path.GetFileNameWithoutExtension(LobbyScenePath);
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        EditorSceneManager.SaveScene(mainMenuScene);

        if (restoreActiveScene && !string.IsNullOrEmpty(activeScene.path))
        {
            EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);
        }
    }

    private static void EnsureBuildSettings()
    {
        string[] scenePaths =
        {
            MainMenuScenePath,
            LobbyScenePath,
            GameScenePath
        };

        EditorBuildSettings.scenes = scenePaths
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToArray();
    }
}
