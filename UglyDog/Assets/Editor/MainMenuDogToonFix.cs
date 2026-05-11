using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class MainMenuDogToonFix
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string DogObjectName = "Menu DOG";
    private const string ToonShaderName = "Custom/ToonLitOutline";
    private const string DogToonMaterialPath = "Assets/ToonURP/Materials/DogToon.mat";
    private const string DogOutlineMaterialPath = "Assets/ToonURP/Materials/DogOutline.mat";

    [MenuItem("Tools/Fix Main Menu Dog Toon")]
    public static void FixMainMenuDogToon()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject dog = GameObject.Find(DogObjectName);
        if (dog == null)
        {
            Debug.LogWarning("Could not find Menu DOG in MainMenu scene.");
            return;
        }

        Material dogToonMaterial = AssetDatabase.LoadAssetAtPath<Material>(DogToonMaterialPath);
        Material dogOutlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(DogOutlineMaterialPath);
        if (dogToonMaterial == null || dogOutlineMaterial == null)
        {
            Debug.LogWarning("DogToon.mat or DogOutline.mat is missing.");
            return;
        }

        ApplyDogToon(dog, dogToonMaterial, dogOutlineMaterial);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("MainMenu Menu DOG now uses DogToon + DogOutline.");
    }

    private static void ApplyDogToon(GameObject dog, Material dogToonMaterial, Material dogOutlineMaterial)
    {
        ToonCharacterSetup setup = dog.GetComponent<ToonCharacterSetup>();
        if (setup == null)
        {
            setup = dog.AddComponent<ToonCharacterSetup>();
        }

        SerializedObject serializedSetup = new SerializedObject(setup);
        serializedSetup.FindProperty("targetRootName").stringValue = dog.name;
        serializedSetup.FindProperty("targetRoot").objectReferenceValue = dog.transform;
        serializedSetup.FindProperty("baseToonMaterial").objectReferenceValue = dogToonMaterial;
        serializedSetup.FindProperty("toonShaderName").stringValue = ToonShaderName;
        serializedSetup.FindProperty("outlineMaterial").objectReferenceValue = dogOutlineMaterial;
        serializedSetup.FindProperty("enableOutline").boolValue = true;
        serializedSetup.FindProperty("preserveExistingMaterialTextures").boolValue = false;
        serializedSetup.FindProperty("baseColor").colorValue = Color.white;
        serializedSetup.FindProperty("shadowColor").colorValue = new Color(0.64f, 0.48f, 0.38f, 1f);
        serializedSetup.FindProperty("shadowThreshold").floatValue = 0.42f;
        serializedSetup.FindProperty("shadowSmoothness").floatValue = 0.04f;
        serializedSetup.FindProperty("rimColor").colorValue = new Color(1f, 0.88f, 0.68f, 1f);
        serializedSetup.FindProperty("rimPower").floatValue = 3.5f;
        serializedSetup.FindProperty("rimStrength").floatValue = 0.28f;
        serializedSetup.FindProperty("outlineColor").colorValue = new Color(0.12f, 0.07f, 0.05f, 1f);
        serializedSetup.FindProperty("outlineWidth").floatValue = 0.012f;
        serializedSetup.ApplyModifiedPropertiesWithoutUndo();

        setup.ApplyToonStyle();

        foreach (Renderer renderer in dog.GetComponentsInChildren<Renderer>(true))
        {
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.receiveShadows = false;
        }

        EditorUtility.SetDirty(dog);
        EditorUtility.SetDirty(setup);
    }
}
