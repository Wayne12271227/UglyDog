using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildZonePrefabAutoAssigner
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string CampPrefabPath = "Assets/prefab/Camp.prefab";
    private const string TowerPrefabPath = "Assets/prefab/Tower.prefab";
    private const string WoodMachinePrefabPath = "Assets/prefab/WoodMachine.prefab";
    private const string StoneMachinePrefabPath = "Assets/prefab/StoneMachine.prefab";

    [MenuItem("Tools/Assign Build Zone Prefabs In SampleScene")]
    public static void AssignSampleScene()
    {
        Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        GameObject camp = AssetDatabase.LoadAssetAtPath<GameObject>(CampPrefabPath);
        GameObject tower = AssetDatabase.LoadAssetAtPath<GameObject>(TowerPrefabPath);
        GameObject woodMachine = AssetDatabase.LoadAssetAtPath<GameObject>(WoodMachinePrefabPath);
        GameObject stoneMachine = AssetDatabase.LoadAssetAtPath<GameObject>(StoneMachinePrefabPath);

        ArcherTowerBuildZone[] zones = Object.FindObjectsOfType<ArcherTowerBuildZone>(true);
        for (int i = 0; i < zones.Length; i++)
        {
            AssignPrefabs(zones[i], tower, woodMachine, stoneMachine, camp);
            EditorUtility.SetDirty(zones[i]);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Assigned build zone visual prefabs to " + zones.Length + " zones in SampleScene.");
    }

    private static void AssignPrefabs(
        ArcherTowerBuildZone zone,
        GameObject tower,
        GameObject woodMachine,
        GameObject stoneMachine,
        GameObject camp)
    {
        SerializedObject serializedZone = new SerializedObject(zone);
        SerializedProperty prefabs = serializedZone.FindProperty("buildingVisualPrefabs");
        prefabs.arraySize = 4;

        SetSlot(prefabs.GetArrayElementAtIndex(0), BuildSiteBuildingType.ArcherTower, tower);
        SetSlot(prefabs.GetArrayElementAtIndex(1), BuildSiteBuildingType.AutoLumber, woodMachine);
        SetSlot(prefabs.GetArrayElementAtIndex(2), BuildSiteBuildingType.AutoQuarry, stoneMachine);
        SetSlot(prefabs.GetArrayElementAtIndex(3), BuildSiteBuildingType.Barracks, camp);

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
