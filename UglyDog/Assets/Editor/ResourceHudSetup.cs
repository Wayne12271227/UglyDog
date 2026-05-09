using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ResourceHudSetup
{
    [MenuItem("Tools/Setup Resource HUD")]
    public static void Setup()
    {
        SetupInternal(true);
    }

    private static void SetupInternal(bool showDialog)
    {
        EnsureResourceManager();

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Game UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Game UI Canvas");

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        ResourceHudUI hud = Object.FindObjectOfType<ResourceHudUI>();
        if (hud == null)
        {
            GameObject hudObject = new GameObject("Resource HUD", typeof(RectTransform), typeof(ResourceHudUI));
            Undo.RegisterCreatedObjectUndo(hudObject, "Create Resource HUD");
            hudObject.transform.SetParent(canvas.transform, false);
            hud = hudObject.GetComponent<ResourceHudUI>();
        }

        ResourceManager manager = Object.FindObjectOfType<ResourceManager>(true);
        if (manager != null)
        {
            SerializedObject serializedHud = new SerializedObject(hud);
            serializedHud.FindProperty("resourceManager").objectReferenceValue = manager;
            AssignDefaultIcon(serializedHud, "coinIcon", "Assets/image/ui/coin1.png");
            AssignDefaultIcon(serializedHud, "woodIcon", "Assets/image/ui/wood1.png");
            AssignDefaultIcon(serializedHud, "stoneIcon", "Assets/image/ui/stone1.png");
            serializedHud.ApplyModifiedProperties();
        }

        EnsureEventSystem();

        EditorUtility.SetDirty(canvas);
        EditorUtility.SetDirty(hud);

        if (showDialog)
        {
            Selection.activeGameObject = hud.gameObject;
            EditorUtility.DisplayDialog("Setup Resource HUD", "Done. Resource HUD was created in the top-right corner.", "OK");
        }
    }

    private static void EnsureResourceManager()
    {
        if (Object.FindObjectOfType<ResourceManager>() != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("ResourceManager", typeof(ResourceManager));
        Undo.RegisterCreatedObjectUndo(managerObject, "Create ResourceManager");
    }

    private static void AssignDefaultIcon(SerializedObject serializedHud, string propertyName, string assetPath)
    {
        SerializedProperty property = serializedHud.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue != null)
        {
            return;
        }

        property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create EventSystem");
    }
}
