using System.Linq;
using System.IO;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UglyDogFusionSetup
{
    private const string DogPrefabPath = "Assets/prefab/DOG.prefab";
    private const string CatPrefabPath = "Assets/prefab/CAT.prefab";
    private const string FusionPrefabLabel = "FusionPrefab";
    private const string PendingSetupRequestPath = "Temp/UglyDogFusionSetup.request";

    [InitializeOnLoadMethod]
    private static void RunPendingSetupRequest()
    {
        if (!File.Exists(PendingSetupRequestPath))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(PendingSetupRequestPath))
            {
                return;
            }

            File.Delete(PendingSetupRequestPath);
            ConfigureCurrentSceneAndPrefab();
        };
    }

    [MenuItem("UglyDog/Photon/Configure Fusion Multiplayer")]
    public static void ConfigureCurrentSceneAndPrefab()
    {
        NetworkObject playerPrefab = ConfigurePlayerPrefab(DogPrefabPath);
        ConfigurePlayerPrefab(CatPrefabPath);
        ConfigureLauncher(playerPrefab);
        DisableScenePrototypePlayers();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RebuildFusionPrefabTable();
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("UglyDog Fusion multiplayer setup complete. Press Play to start AutoHostOrClient.");
    }

    private static NetworkObject ConfigurePlayerPrefab(string prefabPath)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            NetworkObject networkObject = prefabRoot.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                networkObject = prefabRoot.AddComponent<NetworkObject>();
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

        GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        SetFusionPrefabLabel(savedPrefab);
        return savedPrefab.GetComponent<NetworkObject>();
    }

    private static void ConfigureLauncher(NetworkObject playerPrefab)
    {
        GameObject launcherObject = GameObject.Find("Photon Fusion Launcher");
        if (launcherObject == null)
        {
            launcherObject = new GameObject("Photon Fusion Launcher");
        }

        UglyDogFusionLauncher launcher = launcherObject.GetComponent<UglyDogFusionLauncher>();
        if (launcher == null)
        {
            launcher = launcherObject.AddComponent<UglyDogFusionLauncher>();
        }

        SerializedObject serializedLauncher = new SerializedObject(launcher);
        serializedLauncher.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
        serializedLauncher.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(launcher);
        EditorSceneManager.MarkSceneDirty(launcherObject.scene);
    }

    private static void DisableScenePrototypePlayers()
    {
        foreach (CatPlayerController player in Object.FindObjectsOfType<CatPlayerController>())
        {
            if (PrefabUtility.IsPartOfPrefabAsset(player.gameObject))
            {
                continue;
            }

            if (player.gameObject.name.Contains("DOG") || player.gameObject.name.Contains("CAT"))
            {
                player.gameObject.SetActive(false);
                EditorUtility.SetDirty(player.gameObject);
            }
        }
    }

    private static void SetFusionPrefabLabel(Object asset)
    {
        string[] labels = AssetDatabase.GetLabels(asset);
        if (!labels.Contains(FusionPrefabLabel))
        {
            AssetDatabase.SetLabels(asset, labels.Concat(new[] { FusionPrefabLabel }).ToArray());
        }
    }

    private static void RebuildFusionPrefabTable()
    {
        Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
    }
}
