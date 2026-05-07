using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TwoCampPrototypeSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/TwoCampPrototype.unity";
    private const string MaterialFolder = "Assets/matrial/PrototypeArena";
    private const string AutoBuildRequestPath = "Temp/BuildTwoCampPrototype.request";

    [InitializeOnLoadMethod]
    private static void AutoBuildWhenRequested()
    {
        if (!File.Exists(AutoBuildRequestPath))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(AutoBuildRequestPath))
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
        if (File.Exists(AutoBuildRequestPath))
        {
            ConsumeRequestAndBuild();
        }
    }

    private static void ConsumeRequestAndBuild()
    {
        File.Delete(AutoBuildRequestPath);
        BuildTwoCampPrototype();
    }

    [MenuItem("Tools/Build Two Camp Prototype Scene")]
    public static void BuildTwoCampPrototype()
    {
        Directory.CreateDirectory(MaterialFolder);

        Material ground = CreateMaterial("Ground", new Color(0.31f, 0.42f, 0.29f));
        Material lane = CreateMaterial("Lane", new Color(0.47f, 0.42f, 0.33f));
        Material blue = CreateMaterial("BlueCamp", new Color(0.14f, 0.38f, 0.82f));
        Material red = CreateMaterial("RedCamp", new Color(0.77f, 0.19f, 0.16f));
        Material neutral = CreateMaterial("Neutral", new Color(0.86f, 0.72f, 0.38f));
        Material wall = CreateMaterial("LowWall", new Color(0.35f, 0.33f, 0.29f));
        Material wood = CreateMaterial("WoodResource", new Color(0.34f, 0.19f, 0.08f));
        Material stone = CreateMaterial("StoneResource", new Color(0.48f, 0.49f, 0.48f));

        Scene previousActiveScene = SceneManager.GetActiveScene();
        bool canCreateAdditively = previousActiveScene.IsValid() && !string.IsNullOrEmpty(previousActiveScene.path);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, canCreateAdditively ? NewSceneMode.Additive : NewSceneMode.Single);
        scene.name = "TwoCampPrototype";
        SceneManager.SetActiveScene(scene);

        GameObject root = new GameObject("Two Camp Prototype Arena - 70 x 45");

        CreateBox("Ground 70x45", root.transform, Vector3.zero, new Vector3(70f, 0.2f, 45f), ground);
        CreateBox("Main Lane", root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(62f, 0.08f, 8f), lane);
        CreateBox("Upper Flank", root.transform, new Vector3(0f, 0.05f, 14f), new Vector3(52f, 0.08f, 5f), lane);
        CreateBox("Lower Flank", root.transform, new Vector3(0f, 0.05f, -14f), new Vector3(52f, 0.08f, 5f), lane);

        CreateCamp(root.transform, "Blue Camp", -26f, blue, wall);
        CreateCamp(root.transform, "Red Camp", 26f, red, wall);

        CreateBox("Neutral Center Zone", root.transform, new Vector3(0f, 0.08f, 0f), new Vector3(12f, 0.12f, 12f), neutral);
        CreateResourceCluster(root.transform, "Center Stone", new Vector3(0f, 0.8f, 0f), stone, PrimitiveType.Sphere, 5);
        CreateResourceCluster(root.transform, "North Wood", new Vector3(-7f, 0.65f, 16f), wood, PrimitiveType.Cylinder, 4);
        CreateResourceCluster(root.transform, "South Wood", new Vector3(7f, 0.65f, -16f), wood, PrimitiveType.Cylinder, 4);
        CreateResourceCluster(root.transform, "Blue Safe Stone", new Vector3(-20f, 0.65f, -13f), stone, PrimitiveType.Sphere, 3);
        CreateResourceCluster(root.transform, "Red Safe Stone", new Vector3(20f, 0.65f, 13f), stone, PrimitiveType.Sphere, 3);

        CreateBox("Center Cover North", root.transform, new Vector3(0f, 0.7f, 8.5f), new Vector3(10f, 1.4f, 1.2f), wall);
        CreateBox("Center Cover South", root.transform, new Vector3(0f, 0.7f, -8.5f), new Vector3(10f, 1.4f, 1.2f), wall);
        CreateBox("North Choke Left", root.transform, new Vector3(-13f, 0.7f, 9f), new Vector3(1.2f, 1.4f, 7f), wall);
        CreateBox("North Choke Right", root.transform, new Vector3(13f, 0.7f, 9f), new Vector3(1.2f, 1.4f, 7f), wall);
        CreateBox("South Choke Left", root.transform, new Vector3(-13f, 0.7f, -9f), new Vector3(1.2f, 1.4f, 7f), wall);
        CreateBox("South Choke Right", root.transform, new Vector3(13f, 0.7f, -9f), new Vector3(1.2f, 1.4f, 7f), wall);

        CreateLabel("BLUE BASE", new Vector3(-26f, 0.25f, 0f), blue);
        CreateLabel("RED BASE", new Vector3(26f, 0.25f, 0f), red);
        CreateLabel("CONTESTED RESOURCE", new Vector3(0f, 0.25f, 0f), neutral);
        CreateLabel("Recommended play area: 70 x 45 units", new Vector3(0f, 0.25f, -20f), neutral);

        CreateLight();

        EditorSceneManager.SaveScene(scene, ScenePath);
        if (canCreateAdditively)
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        if (canCreateAdditively && previousActiveScene.IsValid())
        {
            SceneManager.SetActiveScene(previousActiveScene);
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Debug.Log("Created " + ScenePath);
    }

    private static void CreateCamp(Transform parent, string name, float x, Material campMaterial, Material wallMaterial)
    {
        Transform camp = new GameObject(name).transform;
        camp.SetParent(parent);

        CreateBox(name + " Spawn Pad", camp, new Vector3(x, 0.12f, 0f), new Vector3(12f, 0.18f, 12f), campMaterial);
        CreateBox(name + " Back Wall", camp, new Vector3(x + Mathf.Sign(x) * 7f, 1f, 0f), new Vector3(1f, 2f, 14f), wallMaterial);
        CreateBox(name + " Top Wall", camp, new Vector3(x, 1f, 7f), new Vector3(14f, 2f, 1f), wallMaterial);
        CreateBox(name + " Bottom Wall", camp, new Vector3(x, 1f, -7f), new Vector3(14f, 2f, 1f), wallMaterial);
        CreateCylinder(name + " Core", camp, new Vector3(x, 1.4f, 0f), new Vector3(2.5f, 1.4f, 2.5f), campMaterial);
        CreateCylinder(name + " Upper Tower", camp, new Vector3(x - Mathf.Sign(x) * 4f, 1.5f, 5f), new Vector3(1.5f, 1.5f, 1.5f), campMaterial);
        CreateCylinder(name + " Lower Tower", camp, new Vector3(x - Mathf.Sign(x) * 4f, 1.5f, -5f), new Vector3(1.5f, 1.5f, 1.5f), campMaterial);
    }

    private static void CreateResourceCluster(Transform parent, string name, Vector3 center, Material material, PrimitiveType shape, int count)
    {
        Transform cluster = new GameObject(name).transform;
        cluster.SetParent(parent);

        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector3 position = center + new Vector3(Mathf.Cos(angle) * 1.6f, 0f, Mathf.Sin(angle) * 1.6f);
            GameObject resource = GameObject.CreatePrimitive(shape);
            resource.name = name + " Resource " + (i + 1);
            resource.transform.SetParent(cluster);
            resource.transform.position = position;
            resource.transform.localScale = shape == PrimitiveType.Cylinder ? new Vector3(0.9f, 1.3f, 0.9f) : Vector3.one * 1.2f;
            resource.GetComponent<Renderer>().sharedMaterial = material;
        }
    }

    private static GameObject CreateBox(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent);
        box.transform.position = position;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    private static GameObject CreateCylinder(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent);
        cylinder.transform.position = position;
        cylinder.transform.localScale = scale;
        cylinder.GetComponent<Renderer>().sharedMaterial = material;
        return cylinder;
    }

    private static void CreateLabel(string text, Vector3 position, Material material)
    {
        GameObject label = new GameObject("Label - " + text);
        label.transform.position = position;
        label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        TextMesh mesh = label.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.characterSize = 0.75f;
        mesh.fontSize = 48;
        mesh.color = material.color;
    }

    private static void CreateLight()
    {
        GameObject lightObject = new GameObject("Prototype Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static Material CreateMaterial(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
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
}
