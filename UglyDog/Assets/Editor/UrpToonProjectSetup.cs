using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class UrpToonProjectSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MaterialsFolder = "Assets/ToonURP/Materials";
    private const string DefaultOutlineMaterialPath = "Assets/ToonURP/Materials/DefaultToonOutline.mat";
    private const string OutlineShaderPath = "Assets/Shaders/URPToonOutline.shader";
    private const string SetupObjectName = "Toon Setup";
    private const string TargetRootName = "CAT";
    private const string ToonShaderName = "Custom/ToonLitOutline";

    private static void TrySetup()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TrySetup;
            return;
        }

        EnsureFolder("Assets/ToonURP");
        EnsureFolder(MaterialsFolder);

        if (!EnsureMaterials())
        {
            EditorApplication.delayCall += TrySetup;
            return;
        }

        ConfigureScene();
        AssetDatabase.SaveAssets();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        var parts = path.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static bool EnsureMaterials()
    {
        var outlineShader = AssetDatabase.LoadAssetAtPath<Shader>(OutlineShaderPath);
        if (outlineShader == null)
        {
            return false;
        }

        var outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultOutlineMaterialPath);
        if (outlineMaterial == null)
        {
            outlineMaterial = new Material(outlineShader);
            AssetDatabase.CreateAsset(outlineMaterial, DefaultOutlineMaterialPath);
        }

        outlineMaterial.shader = outlineShader;
        outlineMaterial.SetColor("_OutlineColor", new Color(0.14f, 0.08f, 0.06f, 1f));
        outlineMaterial.SetFloat("_OutlineWidth", 0.011f);
        EditorUtility.SetDirty(outlineMaterial);
        return true;
    }

    private static void ConfigureScene()
    {
        if (!File.Exists(ScenePath))
        {
            return;
        }

        bool isPlaying = EditorApplication.isPlayingOrWillChangePlaymode;
        var scene = isPlaying ? SceneManager.GetActiveScene() : SceneManager.GetSceneByPath(ScenePath);
        var openedByTool = false;
        if (!isPlaying && !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedByTool = true;
        }

        var outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultOutlineMaterialPath);
        if (outlineMaterial == null)
        {
            return;
        }

        var setup = isPlaying ? FindExistingSetup(scene) : FindOrCreateSetup(scene);
        if (setup == null)
        {
            return;
        }

        var serializedObject = new SerializedObject(setup);
        serializedObject.FindProperty("targetRootName").stringValue = TargetRootName;
        serializedObject.FindProperty("targetRoot").objectReferenceValue = null;
        serializedObject.FindProperty("baseToonMaterial").objectReferenceValue = null;
        serializedObject.FindProperty("toonShaderName").stringValue = ToonShaderName;
        serializedObject.FindProperty("outlineMaterial").objectReferenceValue = outlineMaterial;
        serializedObject.FindProperty("enableOutline").boolValue = true;
        serializedObject.FindProperty("preserveExistingMaterialTextures").boolValue = true;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        var light = Object.FindObjectOfType<Light>();
        if (light != null)
        {
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.94f, 0.84f, 1f);
            light.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            if (!isPlaying)
            {
                EditorUtility.SetDirty(light);
            }
        }

        var camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(0.05f, 1.02f, -4.15f);
            camera.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            camera.backgroundColor = new Color(0.86f, 0.91f, 0.96f, 0f);
            if (!isPlaying)
            {
                EditorUtility.SetDirty(camera);
            }
        }

        setup.ApplyToonStyle();
        if (!isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (!isPlaying && openedByTool)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static ToonCharacterSetup FindOrCreateSetup(Scene scene)
    {
        var existing = FindExistingSetup(scene);
        if (existing != null)
        {
            return existing;
        }

        var setupObject = new GameObject(SetupObjectName);
        SceneManager.MoveGameObjectToScene(setupObject, scene);
        return setupObject.AddComponent<ToonCharacterSetup>();
    }

    private static ToonCharacterSetup FindExistingSetup(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == SetupObjectName)
            {
                var existing = root.GetComponent<ToonCharacterSetup>();
                if (existing != null)
                {
                    return existing;
                }
            }
        }

        return null;
    }
}
