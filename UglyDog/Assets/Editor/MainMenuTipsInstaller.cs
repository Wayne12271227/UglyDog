using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MainMenuTipsInstaller
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string TipsPrefabPath = "Assets/prefab/TIPS.prefab";
    private const string TipsObjectName = "TIPS";

    static MainMenuTipsInstaller()
    {
        EditorApplication.delayCall += InstallIfNeeded;
    }

    [MenuItem("Tools/Install Main Menu TIPS")]
    public static void InstallIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += InstallIfNeeded;
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            return;
        }

        GameObject existingTips = FindSceneObject(scene, TipsObjectName);
        if (existingTips != null)
        {
            ConfigureTips(existingTips);
            return;
        }

        GameObject tipsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TipsPrefabPath);
        if (tipsPrefab == null)
        {
            Debug.LogWarning("Could not find " + TipsPrefabPath + " for MainMenu TIPS.");
            return;
        }

        GameObject tips = PrefabUtility.InstantiatePrefab(tipsPrefab, scene) as GameObject;
        if (tips == null)
        {
            return;
        }

        tips.name = TipsObjectName;
        ConfigureTips(tips);
        tips.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Installed TIPS prefab into MainMenu scene.");
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindChildRecursive(roots[i].transform, objectName);
            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void ConfigureTips(GameObject tips)
    {
        Camera camera = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
        Canvas mainCanvas = Object.FindObjectOfType<MainMenuController>() != null
            ? Object.FindObjectOfType<MainMenuController>().GetComponentInParent<Canvas>()
            : null;
        int sortingOrder = mainCanvas != null ? mainCanvas.sortingOrder + 50 : 70;

        RectTransform rectTransform = tips.transform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }
        else
        {
            tips.transform.localScale = Vector3.one;
        }

        Canvas canvas = tips.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = camera != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.5f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }
    }
}
