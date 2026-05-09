using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DashedAreaDemoBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RequestPath = "Temp/CreateDashedAreaDemo.request";
    private const string AssetFolder = "Assets/matrial/DashedArea";
    private const string MaterialPath = AssetFolder + "/DashedAreaBlack.mat";
    private const string MeshPath = AssetFolder + "/DashedAreaDemoMesh.asset";
    private const string DemoName = "Dashed Area Demo";

    [InitializeOnLoadMethod]
    private static void CreateWhenRequested()
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

            File.Delete(RequestPath);
            CreateDashedAreaDemo();
        };
    }

    [MenuItem("Tools/Create Dashed Area Demo")]
    public static void CreateDashedAreaDemo()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureAssetFolder();

        Mesh mesh = GetOrCreateDashMesh();
        Material material = GetOrCreateMaterial();

        GameObject existing = GameObject.Find(DemoName);
        if (existing != null)
        {
            EnsureInteractiveVisual(existing);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = existing;
            return;
        }

        GameObject demo = new GameObject(DemoName, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
        demo.transform.position = GetDemoPosition();

        MeshFilter filter = demo.GetComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = demo.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        BoxCollider trigger = demo.GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.5f, 0f);
        trigger.size = new Vector3(4.4f, 1.8f, 4.4f);

        EnsureInteractiveVisual(demo);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = demo;
    }

    private static Vector3 GetDemoPosition()
    {
        GameObject stoneArea = GameObject.Find("stone_area");
        if (stoneArea != null)
        {
            Vector3 position = stoneArea.transform.position;
            return new Vector3(position.x + 5f, 0.18f, position.z);
        }

        return new Vector3(0f, 0.18f, 0f);
    }

    private static Mesh GetOrCreateDashMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh != null)
        {
            return mesh;
        }

        mesh = CreateDashMesh(2f, 0.08f, 32, 0.52f);
        AssetDatabase.CreateAsset(mesh, MeshPath);
        return mesh;
    }

    private static Mesh CreateDashMesh(float radius, float width, int dashCount, float dashFill)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        float angleStep = Mathf.PI * 2f / dashCount;
        float halfWidth = width * 0.5f;

        for (int i = 0; i < dashCount; i++)
        {
            float startAngle = i * angleStep;
            float endAngle = startAngle + angleStep * dashFill;
            Vector3 start = new Vector3(Mathf.Cos(startAngle) * radius, 0f, Mathf.Sin(startAngle) * radius);
            Vector3 end = new Vector3(Mathf.Cos(endAngle) * radius, 0f, Mathf.Sin(endAngle) * radius);
            Vector3 direction = (end - start).normalized;
            Vector3 side = new Vector3(-direction.z, 0f, direction.x) * halfWidth;

            int baseIndex = vertices.Count;
            vertices.Add(start - side);
            vertices.Add(start + side);
            vertices.Add(end - side);
            vertices.Add(end + side);

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 3);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
            triangles.Add(baseIndex + 1);
        }

        Mesh mesh = new Mesh { name = "Dashed Area Demo Mesh" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Material GetOrCreateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        SetMaterialColor(material, Color.black);
        SetMaterialFloat(material, "_Cull", 0f);
        SetMaterialFloat(material, "_ZWrite", 0f);
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureInteractiveVisual(GameObject area)
    {
        InteractiveAreaVisual visual = area.GetComponent<InteractiveAreaVisual>();
        if (visual == null)
        {
            visual = area.AddComponent<InteractiveAreaVisual>();
        }

        visual.RefreshVisuals();
        EditorUtility.SetDirty(visual);
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void SetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void EnsureAssetFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/matrial"))
        {
            AssetDatabase.CreateFolder("Assets", "matrial");
        }

        if (!AssetDatabase.IsValidFolder(AssetFolder))
        {
            AssetDatabase.CreateFolder("Assets/matrial", "DashedArea");
        }
    }
}
