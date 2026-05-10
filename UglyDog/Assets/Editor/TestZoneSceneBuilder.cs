using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TestZoneSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MaterialFolder = "Assets/matrial/TestZones";

    public static void BuildTestZones()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        DeleteExisting("TEST_WoodZone");
        DeleteExisting("TEST_StoneZone");
        DeleteExisting("TEST_BuildZone");
        DeleteExisting("TEST_SellWoodZone");
        DeleteExisting("TEST_SellStoneZone");

        Material woodMaterial = GetOrCreateMaterial("TEST_Wood_Green.mat", new Color(0.12f, 0.72f, 0.2f, 1f));
        Material stoneMaterial = GetOrCreateMaterial("TEST_Stone_Gray.mat", new Color(0.48f, 0.5f, 0.5f, 1f));
        Material buildMaterial = GetOrCreateMaterial("TEST_Build_Yellow.mat", new Color(1f, 0.82f, 0.12f, 1f));
        Material sellWoodMaterial = GetOrCreateMaterial("TEST_SellWood_Gold.mat", new Color(0.98f, 0.6f, 0.12f, 1f));
        Material sellStoneMaterial = GetOrCreateMaterial("TEST_SellStone_Gold.mat", new Color(0.95f, 0.72f, 0.2f, 1f));

        GameObject wood = CreateZone("TEST_WoodZone", new Vector3(-3f, 0.08f, -4f), woodMaterial);
        wood.AddComponent<WoodGatheringZone>();

        GameObject stone = CreateZone("TEST_StoneZone", new Vector3(0f, 0.08f, -4f), stoneMaterial);
        stone.AddComponent<StoneGatheringZone>();

        GameObject build = CreateZone("TEST_BuildZone", new Vector3(3f, 0.08f, -4f), buildMaterial);
        build.AddComponent<BuildZone>();

        GameObject sellWood = CreateZone("TEST_SellWoodZone", new Vector3(-1.5f, 0.08f, -7f), sellWoodMaterial);
        ConfigureSellZone(sellWood.AddComponent<ResourceSellZone>(), ResourceType.Wood, 1, 1);
        CreateLabel(sellWood.transform, "Sell Wood\n+1 Coin", new Vector3(0f, 1.3f, 0f));

        GameObject sellStone = CreateZone("TEST_SellStoneZone", new Vector3(1.5f, 0.08f, -7f), sellStoneMaterial);
        ConfigureSellZone(sellStone.AddComponent<ResourceSellZone>(), ResourceType.Stone, 1, 2);
        CreateLabel(sellStone.transform, "Sell Stone\n+2 Coins", new Vector3(0f, 1.3f, 0f));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static GameObject CreateZone(string name, Vector3 position, Material material)
    {
        GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        zone.name = name;
        zone.transform.position = position;
        zone.transform.localScale = new Vector3(2f, 0.16f, 2f);

        BoxCollider collider = zone.GetComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(1f, 2.5f, 1f);
        collider.center = new Vector3(0f, 0.5f, 0f);

        MeshRenderer renderer = zone.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        return zone;
    }

    private static void ConfigureSellZone(ResourceSellZone sellZone, ResourceType resourceToSell, int resourcePerTick, int coinsPerTick)
    {
        SerializedObject serializedZone = new SerializedObject(sellZone);
        serializedZone.FindProperty("resourceToSell").enumValueIndex = (int)resourceToSell;
        serializedZone.FindProperty("resourcePerTick").intValue = resourcePerTick;
        serializedZone.FindProperty("coinsPerTick").intValue = coinsPerTick;
        serializedZone.FindProperty("tickInterval").floatValue = 0.5f;
        serializedZone.FindProperty("sellImmediatelyOnEnter").boolValue = true;
        serializedZone.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateLabel(Transform parent, string text, Vector3 localPosition)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent);
        labelObject.transform.localPosition = localPosition;
        labelObject.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = text;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.characterSize = 0.22f;
        label.fontSize = 42;
        label.color = Color.black;
    }

    private static Material GetOrCreateMaterial(string fileName, Color color)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            Directory.CreateDirectory(MaterialFolder);
        }

        string path = $"{MaterialFolder}/{fileName}";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void DeleteExisting(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
    }
}
