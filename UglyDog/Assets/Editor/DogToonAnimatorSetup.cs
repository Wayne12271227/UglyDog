using UnityEditor;
using UnityEngine;

public static class CharacterToonSetupTool
{
    private const string ToonShaderName = "Custom/ToonLitOutline";
    private const string OutlineShaderName = "Custom/URPToonOutline";
    private const string MaterialsFolder = "Assets/ToonURP/Materials";
    private const string OutlineMaterialPath = MaterialsFolder + "/DefaultToonOutline.mat";

    [MenuItem("Tools/Set Toon")]
    public static void SetToonForSelectedCharacters()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("Set Toon", "Please select one or more character roots first.", "OK");
            return;
        }

        Material outlineMaterial = EnsureOutlineMaterial();
        foreach (GameObject selectedObject in selectedObjects)
        {
            ApplyToonSetup(selectedObject, outlineMaterial);
            EditorUtility.SetDirty(selectedObject);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Set Toon", "Done. Toon setup was applied to selected character roots.", "OK");
    }

    private static void ApplyToonSetup(GameObject characterRoot, Material outlineMaterial)
    {
        ToonCharacterSetup setup = characterRoot.GetComponent<ToonCharacterSetup>();
        if (setup == null)
        {
            setup = Undo.AddComponent<ToonCharacterSetup>(characterRoot);
        }

        SerializedObject serializedSetup = new SerializedObject(setup);
        serializedSetup.FindProperty("targetRootName").stringValue = characterRoot.name;
        serializedSetup.FindProperty("targetRoot").objectReferenceValue = characterRoot.transform;
        serializedSetup.FindProperty("baseToonMaterial").objectReferenceValue = null;
        serializedSetup.FindProperty("toonShaderName").stringValue = ToonShaderName;
        serializedSetup.FindProperty("outlineMaterial").objectReferenceValue = outlineMaterial;
        serializedSetup.FindProperty("enableOutline").boolValue = true;
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
        EditorUtility.SetDirty(setup);
    }

    private static Material EnsureOutlineMaterial()
    {
        EnsureFolder(MaterialsFolder);

        Shader outlineShader = Shader.Find(OutlineShaderName);
        if (outlineShader == null)
        {
            outlineShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/URPToonOutline.shader");
        }

        if (outlineShader == null)
        {
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
        if (material == null)
        {
            material = new Material(outlineShader);
            AssetDatabase.CreateAsset(material, OutlineMaterialPath);
        }

        material.shader = outlineShader;
        SetColorIfAvailable(material, "_OutlineColor", new Color(0.14f, 0.08f, 0.06f, 1f));
        SetFloatIfAvailable(material, "_OutlineWidth", 0.011f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetColorIfAvailable(Material material, string propertyName, Color value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static void SetFloatIfAvailable(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
