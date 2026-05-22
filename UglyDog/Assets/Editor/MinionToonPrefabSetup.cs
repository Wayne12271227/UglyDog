using UnityEditor;
using UnityEngine;

public static class MinionToonPrefabSetup
{
    private const string AutoApplyRequestPath = "Temp/ApplyMinionToonPrefabs.request";
    private const string AutoApplyRequestFileName = "ApplyMinionToonPrefabs.request";
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
        string requestPath = GetAutoApplyRequestPath();
        if (!System.IO.File.Exists(requestPath) && !System.IO.File.Exists(AutoApplyRequestPath))
        {
            return;
        }

        if (System.IO.File.Exists(requestPath))
        {
            System.IO.File.Delete(requestPath);
        }

        if (System.IO.File.Exists(AutoApplyRequestPath))
        {
            System.IO.File.Delete(AutoApplyRequestPath);
        }

        ApplyToonToMinionPrefabs();
    }

    private static string GetAutoApplyRequestPath()
    {
        string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
        return System.IO.Path.Combine(projectRoot, "Temp", AutoApplyRequestFileName);
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
            SerializedProperty toonShaderName = serializedSetup.FindProperty("toonShaderName");
            if (string.IsNullOrWhiteSpace(toonShaderName.stringValue))
            {
                toonShaderName.stringValue = ToonShaderName;
            }

            SerializedProperty setupOutlineMaterial = serializedSetup.FindProperty("outlineMaterial");
            if (setupOutlineMaterial.objectReferenceValue == null)
            {
                setupOutlineMaterial.objectReferenceValue = outlineMaterial;
            }

            if (setupOutlineMaterial.objectReferenceValue != null)
            {
                serializedSetup.FindProperty("enableOutline").boolValue = true;
            }

            serializedSetup.FindProperty("preserveExistingMaterialTextures").boolValue = true;
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
