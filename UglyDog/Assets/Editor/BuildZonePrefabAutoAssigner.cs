using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildZonePrefabAutoAssigner
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string CampPrefabPath = "Assets/prefab/building/Camp.prefab";
    private const string TowerPrefabPath = "Assets/prefab/building/Tower.prefab";
    private const string WoodMachinePrefabPath = "Assets/prefab/building/WoodMachine.prefab";
    private const string StoneMachinePrefabPath = "Assets/prefab/building/StoneMachine.prefab";
    private const string BuildShopPrefabPath = "Assets/prefab/buildCanvas 1.prefab";
    private const string PendingAssignRequestPath = "Temp/AssignBuildZonePrefabs.request";

    [InitializeOnLoadMethod]
    private static void RunPendingAssignRequest()
    {
        string requestPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, PendingAssignRequestPath);
        if (!File.Exists(requestPath))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(requestPath))
            {
                return;
            }

            File.Delete(requestPath);
            AssignSampleScene();
        };
    }

    [MenuItem("Tools/Assign Build Zone Prefabs In SampleScene")]
    public static void AssignSampleScene()
    {
        ConfigureBuildShopPrefab();

        Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        GameObject camp = AssetDatabase.LoadAssetAtPath<GameObject>(CampPrefabPath);
        GameObject tower = AssetDatabase.LoadAssetAtPath<GameObject>(TowerPrefabPath);
        GameObject woodMachine = AssetDatabase.LoadAssetAtPath<GameObject>(WoodMachinePrefabPath);
        GameObject stoneMachine = AssetDatabase.LoadAssetAtPath<GameObject>(StoneMachinePrefabPath);
        GameObject buildShop = AssetDatabase.LoadAssetAtPath<GameObject>(BuildShopPrefabPath);

        ArcherTowerBuildZone[] zones = Object.FindObjectsOfType<ArcherTowerBuildZone>(true);
        for (int i = 0; i < zones.Length; i++)
        {
            AssignPrefabs(zones[i], tower, woodMachine, stoneMachine, camp, buildShop);
            EditorUtility.SetDirty(zones[i]);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Assigned build zone visual prefabs and build shop UI to " + zones.Length + " zones in SampleScene.");
    }

    private static void ConfigureBuildShopPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(BuildShopPrefabPath);
        try
        {
            foreach (UpgradeShopUI upgradeShop in prefabRoot.GetComponentsInChildren<UpgradeShopUI>(true))
            {
                Object.DestroyImmediate(upgradeShop);
            }

            if (prefabRoot.GetComponent<BuildShopUI>() == null)
            {
                prefabRoot.AddComponent<BuildShopUI>();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, BuildShopPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void AssignPrefabs(
        ArcherTowerBuildZone zone,
        GameObject tower,
        GameObject woodMachine,
        GameObject stoneMachine,
        GameObject camp,
        GameObject buildShop)
    {
        SerializedObject serializedZone = new SerializedObject(zone);
        SerializedProperty prefabs = serializedZone.FindProperty("buildingVisualPrefabs");
        prefabs.arraySize = 4;

        SetSlot(prefabs.GetArrayElementAtIndex(0), BuildSiteBuildingType.ArcherTower, tower);
        SetSlot(prefabs.GetArrayElementAtIndex(1), BuildSiteBuildingType.AutoLumber, woodMachine);
        SetSlot(prefabs.GetArrayElementAtIndex(2), BuildSiteBuildingType.AutoQuarry, stoneMachine);
        SetSlot(prefabs.GetArrayElementAtIndex(3), BuildSiteBuildingType.Barracks, camp);
        serializedZone.FindProperty("buildShopPrefab").objectReferenceValue = buildShop;

        serializedZone.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSlot(SerializedProperty slot, BuildSiteBuildingType type, GameObject prefab)
    {
        slot.FindPropertyRelative("type").enumValueIndex = (int)type;
        slot.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        slot.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
        slot.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
        slot.FindPropertyRelative("localScale").vector3Value = Vector3.one;
    }
}
