#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DemoPrefabCreator
{
    private const string PrefabFolder = "Assets/Resources/DemoPrefabs";
    private const string MaterialFolder = "Assets/Resources/DemoPrefabs/Materials";

    [MenuItem("Tools/UglyDog/Create Demo Prefabs")]
    public static void CreateDemoPrefabs()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "DemoPrefabs");
        EnsureFolder("Assets/Resources/DemoPrefabs", "Materials");

        var groundMaterial = CreateMaterial("Ground", new Color(0.82f, 0.92f, 0.82f));
        var playerMaterial = CreateMaterial("Player", new Color(0.91f, 0.56f, 0.36f));
        var zoneMaterial = CreateMaterial("Zone", new Color(0.42f, 0.63f, 0.87f));
        var treeMaterial = CreateMaterial("Tree", new Color(0.29f, 0.56f, 0.30f));

        CreateGroundPrefab(groundMaterial);
        CreatePlayerPrefab(playerMaterial);
        CreateWoodZonePrefab(zoneMaterial, treeMaterial);
        CreateCameraPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Demo prefabs created under Assets/Resources/DemoPrefabs.");
    }

    private static void CreateGroundPrefab(Material material)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "DemoGround";
        ground.transform.localScale = new Vector3(2f, 1f, 2f);
        ground.GetComponent<Renderer>().sharedMaterial = material;

        SavePrefab(ground, "DemoGround.prefab");
    }

    private static void CreatePlayerPrefab(Material material)
    {
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "DemoPlayer";

        var controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.5f;
        controller.center = new Vector3(0f, 1f, 0f);
        controller.stepOffset = 0.2f;

        player.AddComponent<PlayerMovementDemo>();
        player.AddComponent<PlayerResourceInventory>();
        player.AddComponent<DemoHud>();

        player.GetComponent<Renderer>().sharedMaterial = material;

        SavePrefab(player, "DemoPlayer.prefab");
    }

    private static void CreateWoodZonePrefab(Material zoneMaterial, Material treeMaterial)
    {
        var zoneRoot = new GameObject("WoodGatherZone");

        var triggerCollider = zoneRoot.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.center = new Vector3(0f, 1f, 0f);
        triggerCollider.size = new Vector3(4f, 2f, 4f);

        var gatherZone = zoneRoot.AddComponent<ResourceGatherZone>();
        gatherZone.gatherAmount = 5;
        gatherZone.gatherInterval = 1f;

        var zoneVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        zoneVisual.name = "ZoneVisual";
        zoneVisual.transform.SetParent(zoneRoot.transform, false);
        zoneVisual.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        zoneVisual.transform.localScale = new Vector3(4f, 0.4f, 4f);
        zoneVisual.GetComponent<Renderer>().sharedMaterial = zoneMaterial;

        var zoneVisualCollider = zoneVisual.GetComponent<Collider>();
        if (zoneVisualCollider != null)
        {
            Object.DestroyImmediate(zoneVisualCollider);
        }

        var tree = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tree.name = "TreeVisual";
        tree.transform.SetParent(zoneRoot.transform, false);
        tree.transform.localPosition = new Vector3(0f, 1f, 0f);
        tree.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
        tree.GetComponent<Renderer>().sharedMaterial = treeMaterial;

        var treeCollider = tree.GetComponent<Collider>();
        if (treeCollider != null)
        {
            Object.DestroyImmediate(treeCollider);
        }

        SavePrefab(zoneRoot, "WoodGatherZone.prefab");
    }

    private static void CreateCameraPrefab()
    {
        var cameraObject = new GameObject("DemoCamera");
        cameraObject.tag = "MainCamera";

        var camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1000f;

        var followCamera = cameraObject.AddComponent<SimpleFollowCamera>();
        followCamera.offset = new Vector3(0f, 10f, -8f);
        followCamera.smoothTime = 0.12f;

        SavePrefab(cameraObject, "DemoCamera.prefab");
    }

    private static Material CreateMaterial(string name, Color color)
    {
        var materialPath = Path.Combine(MaterialFolder, name + ".mat").Replace("\\", "/");
        var existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (existing != null)
        {
            existing.color = color;
            return existing;
        }

        var material = new Material(Shader.Find("Standard"));
        material.color = color;
        AssetDatabase.CreateAsset(material, materialPath);
        return material;
    }

    private static void SavePrefab(GameObject source, string fileName)
    {
        var prefabPath = Path.Combine(PrefabFolder, fileName).Replace("\\", "/");
        PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
        Object.DestroyImmediate(source);
    }

    private static void EnsureFolder(string parentPath, string folderName)
    {
        var fullPath = Path.Combine(parentPath, folderName).Replace("\\", "/");
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }
}
#endif
