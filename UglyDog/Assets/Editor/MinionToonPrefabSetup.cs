using UnityEditor;
using UnityEngine;

public static class MinionToonPrefabSetup
{
    private const string AutoApplyRequestPath = "Temp/ApplyMinionToonPrefabs.request";
    private const string ToonShaderName = "Custom/ToonLitOutline";
    private const string MaterialsFolder = "Assets/ToonURP/Materials";
    private const string DefaultOutlineMaterialPath = MaterialsFolder + "/DefaultToonOutline.mat";
    private const string DogOutlineMaterialPath = MaterialsFolder + "/DogOutline.mat";

    private static readonly string[] CatPrefabPaths =
    {
        "Assets/prefab/cat_melee.prefab",
        "Assets/prefab/cat_ranged.prefab"
    };

    private static readonly string[] DogPrefabPaths =
    {
        "Assets/prefab/dog_melee.prefab",
        "Assets/prefab/dog_ranged.prefab"
    };

    static MinionToonPrefabSetup()
    {
        EditorApplication.delayCall += RunWhenRequested;
    }

    private static void RunWhenRequested()
    {
        if (!System.IO.File.Exists(AutoApplyRequestPath))
        {
            return;
        }

        System.IO.File.Delete(AutoApplyRequestPath);
        ApplyToonToMinionPrefabs();
    }

    [MenuItem("Tools/Minions/Apply Toon To Minion Prefabs")]
    public static void ApplyToonToMinionPrefabs()
    {
        Material defaultOutline = AssetDatabase.LoadAssetAtPath<Material>(DefaultOutlineMaterialPath);
        Material dogOutline = AssetDatabase.LoadAssetAtPath<Material>(DogOutlineMaterialPath);
        if (dogOutline == null)
        {
            dogOutline = defaultOutline;
        }

        foreach (string prefabPath in CatPrefabPaths)
        {
            ApplyToonToPrefab(prefabPath, defaultOutline);
        }

        foreach (string prefabPath in DogPrefabPaths)
        {
            ApplyToonToPrefab(prefabPath, dogOutline);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Applied toon setup to cat_melee, cat_ranged, dog_melee, and dog_ranged prefabs.");
    }

    private static void ApplyToonToPrefab(string prefabPath, Material outlineMaterial)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot == null)
        {
            Debug.LogWarning("Could not load minion prefab: " + prefabPath);
            return;
        }

        try
        {
            ToonCharacterSetup setup = prefabRoot.GetComponent<ToonCharacterSetup>();
            if (setup == null)
            {
                setup = prefabRoot.AddComponent<ToonCharacterSetup>();
            }

            SerializedObject serializedSetup = new SerializedObject(setup);
            serializedSetup.FindProperty("targetRootName").stringValue = prefabRoot.name;
            serializedSetup.FindProperty("targetRoot").objectReferenceValue = prefabRoot.transform;
            serializedSetup.FindProperty("baseToonMaterial").objectReferenceValue = null;
            serializedSetup.FindProperty("toonShaderName").stringValue = ToonShaderName;
            serializedSetup.FindProperty("outlineMaterial").objectReferenceValue = outlineMaterial;
            serializedSetup.FindProperty("enableOutline").boolValue = outlineMaterial != null;
            serializedSetup.FindProperty("preserveExistingMaterialTextures").boolValue = true;
            serializedSetup.FindProperty("baseColor").colorValue = Color.white;
            serializedSetup.FindProperty("shadowColor").colorValue = new Color(0.72f, 0.62f, 0.58f, 1f);
            serializedSetup.FindProperty("shadowThreshold").floatValue = 0.5f;
            serializedSetup.FindProperty("shadowSmoothness").floatValue = 0.05f;
            serializedSetup.FindProperty("rimColor").colorValue = new Color(1f, 0.95f, 0.9f, 1f);
            serializedSetup.FindProperty("rimPower").floatValue = 3f;
            serializedSetup.FindProperty("rimStrength").floatValue = 0.2f;
            serializedSetup.FindProperty("outlineColor").colorValue = new Color(0.14f, 0.08f, 0.06f, 1f);
            serializedSetup.FindProperty("outlineWidth").floatValue = 0.011f;
            serializedSetup.ApplyModifiedPropertiesWithoutUndo();

            setup.ApplyToonStyle();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
