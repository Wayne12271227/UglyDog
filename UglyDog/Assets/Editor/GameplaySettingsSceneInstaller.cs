using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class GameplaySettingsSceneInstaller
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string SettingsPrefabPath = "Assets/prefab/Settings Panel.prefab";
    private const string RequestPath = "Temp/InstallGameplaySettingsUI.request";

    [InitializeOnLoadMethod]
    private static void InstallWhenRequested()
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

            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorApplication.update += InstallWhenEditorLeavesPlayMode;
                return;
            }

            TryInstallAndClearRequest();
        };
    }

    private static void InstallWhenEditorLeavesPlayMode()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            return;
        }

        EditorApplication.update -= InstallWhenEditorLeavesPlayMode;
        if (File.Exists(RequestPath))
        {
            TryInstallAndClearRequest();
        }
    }

    private static void TryInstallAndClearRequest()
    {
        Install();
        if (File.Exists(RequestPath))
        {
            File.Delete(RequestPath);
        }
    }

    [MenuItem("Tools/Install Gameplay Settings UI")]
    public static void Install()
    {
        EditorSceneManager.OpenScene(ScenePath);

        EnsureEventSystem();
        Canvas canvas = FindOrCreateCanvas();
        Button settingsButton = FindOrCreateSettingsButton(canvas.transform);
        SettingsPanelUI settingsPanel = FindOrCreateSettingsPanel(canvas.transform);
        GameplaySettingsController controller = FindOrCreateController();

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("settingsButton").objectReferenceValue = settingsButton;
        serializedController.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        settingsPanel.SetExitGameButtonVisible(true);
        settingsPanel.gameObject.SetActive(false);

        EditorUtility.SetDirty(settingsButton);
        EditorUtility.SetDirty(settingsPanel);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = settingsButton.gameObject;
        Debug.Log("Gameplay settings UI is now in SampleScene and editable before Play.");
    }

    private static Canvas FindOrCreateCanvas()
    {
        Canvas canvas = FindSceneComponentByName<Canvas>("Gameplay Settings Canvas");
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Gameplay Settings Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        return canvas;
    }

    private static Button FindOrCreateSettingsButton(Transform parent)
    {
        Button button = FindSceneComponentByName<Button>("Gameplay Settings Button");
        if (button != null)
        {
            return button;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject buttonObject = new GameObject("Gameplay Settings Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(130f, 48f);
        rect.anchoredPosition = new Vector2(-22f, -22f);
        rect.localScale = Vector3.one;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.74f, 0.48f, 0.24f, 0.96f);

        button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        if (buttonObject.GetComponent<ButtonHoverTint>() == null)
        {
            buttonObject.AddComponent<ButtonHoverTint>();
        }

        CreateText(rect, font, "\u8a2d\u5b9a", 28, FontStyle.Bold);
        return button;
    }

    private static SettingsPanelUI FindOrCreateSettingsPanel(Transform parent)
    {
        SettingsPanelUI panel = FindSceneComponentByName<SettingsPanelUI>("Gameplay Settings Panel");
        if (panel == null)
        {
            panel = FindSceneComponentByName<SettingsPanelUI>("settings(in game)");
        }

        if (panel == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
            GameObject panelObject = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab)
                : new GameObject("Gameplay Settings Panel", typeof(RectTransform), typeof(SettingsPanelUI));

            panelObject.name = "Gameplay Settings Panel";
            panelObject.transform.SetParent(parent, false);
            panel = panelObject.GetComponent<SettingsPanelUI>();
            if (panel == null)
            {
                panel = panelObject.AddComponent<SettingsPanelUI>();
            }
        }
        else if (panel.transform.parent != parent)
        {
            panel.transform.SetParent(parent, false);
        }

        RectTransform rect = panel.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        return panel;
    }

    private static GameplaySettingsController FindOrCreateController()
    {
        GameplaySettingsController controller = FindSceneComponentByName<GameplaySettingsController>("Gameplay Settings Controller");
        if (controller != null)
        {
            return controller;
        }

        GameObject controllerObject = new GameObject("Gameplay Settings Controller");
        return controllerObject.AddComponent<GameplaySettingsController>();
    }

    private static void EnsureEventSystem()
    {
        if (FindSceneComponent<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private static T FindSceneComponentByName<T>(string objectName) where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].gameObject.scene.IsValid() && components[i].name == objectName)
            {
                return components[i];
            }
        }

        return null;
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].gameObject.scene.IsValid())
            {
                return components[i];
            }
        }

        return null;
    }

    private static Text CreateText(Transform parent, Font font, string value, int size, FontStyle style)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
