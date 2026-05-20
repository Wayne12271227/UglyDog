using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CatMinionAnimationTestSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/catScene.unity";
    private const string RigName = "Cat Minion Animation Test Rig";
    private const string RequestPath = "Temp/BuildCatMinionAnimationTest.request";
    private const string CatMeleeVisualPath = "Assets/low_poly_model/minion/cat_minion01/tripo_convert_3492f0fb-46be-4020-96b6-094a94d82626.fbx";
    private const string CatRangedVisualPath = "Assets/low_poly_model/minion/cat_minion02/tripo_convert_741097ac-70c2-41ba-b2ca-ceb4316fb90c.fbx";

    [InitializeOnLoadMethod]
    private static void AutoBuildWhenRequested()
    {
        if (!File.Exists(RequestPath))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(RequestPath))
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update += BuildWhenEditorLeavesPlayMode;
                return;
            }

            ConsumeRequestAndBuild();
        };
    }

    private static void BuildWhenEditorLeavesPlayMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EditorApplication.update -= BuildWhenEditorLeavesPlayMode;
        if (File.Exists(RequestPath))
        {
            ConsumeRequestAndBuild();
        }
    }

    private static void ConsumeRequestAndBuild()
    {
        File.Delete(RequestPath);
        BuildCatSceneTest();
    }

    [MenuItem("Tools/Minions/Build Cat Animation Test Scene")]
    public static void BuildCatSceneTest()
    {
        EditorSceneManager.OpenScene(ScenePath);

        DestroyIfFound(RigName);
        DestroyIfFound("catMinion01");
        DestroyIfFound("catMinion02");

        GameObject rig = new GameObject(RigName);

        CreateGround(rig.transform);
        ConfigureCamera();
        ConfigureLight();

        GameObject meleePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CatMeleeVisualPath);
        GameObject rangedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CatRangedVisualPath);

        CreateDemoUnit("catMinion01 Walk Attack Demo", meleePrefab, new Vector3(-2.2f, 0f, -0.7f), 1.15f, rig.transform);
        CreateDemoUnit("catMinion02 Ranged Walk Attack Demo", rangedPrefab, new Vector3(-2.2f, 0f, 1.15f), 1.05f, rig.transform);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Cat minion animation test scene rebuilt: " + ScenePath);
    }

    private static void CreateDemoUnit(string name, GameObject visualPrefab, Vector3 position, float targetHeight, Transform parent)
    {
        GameObject unit = new GameObject(name);
        unit.transform.SetParent(parent, false);
        unit.transform.position = position;
        unit.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);

        if (visualPrefab == null)
        {
            Debug.LogWarning("Missing minion visual prefab for " + name);
            return;
        }

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, unit.transform);
        visual.name = "Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        FitVisual(visual.transform, targetHeight);

        MinionVisualAnimator visualAnimator = unit.AddComponent<MinionVisualAnimator>();
        visualAnimator.Initialize(visual.transform);
        unit.AddComponent<MinionAnimationDemoDriver>();

        CreateLabel(name.Contains("01") ? "01 Melee" : "02 Ranged", unit.transform);
        EditorUtility.SetDirty(unit);
    }

    private static void FitVisual(Transform visualRoot, float targetHeight)
    {
        Bounds bounds;
        if (!TryGetRendererBounds(visualRoot, out bounds) || bounds.size.y <= 0.001f)
        {
            return;
        }

        visualRoot.localScale *= targetHeight / bounds.size.y;

        if (!TryGetRendererBounds(visualRoot, out bounds))
        {
            return;
        }

        Vector3 worldOffset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        visualRoot.localPosition += visualRoot.parent.InverseTransformVector(worldOffset);
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(root.position, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return hasBounds;
    }

    private static void CreateGround(Transform parent)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Animation Test Ground";
        ground.transform.SetParent(parent, false);
        ground.transform.position = new Vector3(0.2f, -0.06f, 0.2f);
        ground.transform.localScale = new Vector3(6.2f, 0.08f, 4f);

        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.name = "Cat Minion Test Ground";
            material.color = new Color(0.25f, 0.33f, 0.25f, 1f);
            material.SetColor("_BaseColor", material.color);
            renderer.sharedMaterial = material;
        }
    }

    private static void CreateLabel(string text, Transform parent)
    {
        GameObject labelObject = new GameObject(text + " Label");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        labelObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = text;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.characterSize = 0.14f;
        label.fontSize = 32;
        label.color = Color.white;
    }

    private static void ConfigureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        camera.transform.position = new Vector3(2.7f, 2.1f, -5.3f);
        camera.transform.rotation = Quaternion.Euler(18f, -23f, 0f);
        camera.fieldOfView = 45f;
        camera.clearFlags = CameraClearFlags.Skybox;
    }

    private static void ConfigureLight()
    {
        Light light = Object.FindObjectOfType<Light>();
        if (light == null)
        {
            GameObject lightObject = new GameObject("Directional Light");
            light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
        }

        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        light.intensity = 1.4f;
    }

    private static void DestroyIfFound(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        if (found != null)
        {
            Object.DestroyImmediate(found);
        }
    }
}
