using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class VictoryResultPrefabBuilder
{
    private const string PrefabPath = "Assets/prefab/VictoryResultCanvas.prefab";

    static VictoryResultPrefabBuilder()
    {
        EditorApplication.delayCall += CreateIfMissing;
    }

    [MenuItem("Tools/Minions/Rebuild Victory Result Prefab")]
    public static void Rebuild()
    {
        CreatePrefab(true);
    }

    private static void CreateIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            return;
        }

        CreatePrefab(false);
    }

    private static void CreatePrefab(bool replaceExisting)
    {
        if (!replaceExisting && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            return;
        }

        GameObject root = new GameObject("Victory Result Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(VictoryResultView));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject shadeObject = CreateRect("Result Shade", root.transform, Vector2.zero, Vector2.one);
        Image shade = shadeObject.AddComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.62f);
        shade.raycastTarget = true;

        GameObject textObject = CreateRect("Result Text", shadeObject.transform, new Vector2(0.12f, 0.62f), new Vector2(0.88f, 0.84f));
        Text text = textObject.AddComponent<Text>();
        text.font = LoadReadableFont();
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 78;
        text.fontStyle = FontStyle.Bold;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.color = Color.white;

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(4f, -4f);

        GameObject characterObject = CreateRect("Victory Character Image", shadeObject.transform, new Vector2(0.18f, 0.22f), new Vector2(0.82f, 0.61f));
        RawImage characterImage = characterObject.AddComponent<RawImage>();
        characterImage.color = Color.white;
        characterImage.raycastTarget = false;
        characterImage.enabled = false;

        GameObject buttonObject = CreateRect("Return Main Menu Button", shadeObject.transform, new Vector2(0.39f, 0.12f), new Vector2(0.61f, 0.21f));
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(1f, 1f, 1f, 0.92f);
        Button button = buttonObject.AddComponent<Button>();

        GameObject buttonTextObject = CreateRect("Text", buttonObject.transform, Vector2.zero, Vector2.one);
        Text buttonText = buttonTextObject.AddComponent<Text>();
        buttonText.font = LoadReadableFont();
        buttonText.text = "\u8fd4\u56de\u4e3b\u9078\u55ae";
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.fontSize = 34;
        buttonText.fontStyle = FontStyle.Bold;
        buttonText.color = new Color(0.08f, 0.08f, 0.1f, 1f);
        buttonText.raycastTarget = false;

        VictoryResultView view = root.GetComponent<VictoryResultView>();
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("resultText").objectReferenceValue = text;
        serializedView.FindProperty("characterImage").objectReferenceValue = characterImage;
        serializedView.FindProperty("mainMenuButton").objectReferenceValue = button;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static GameObject CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        gameObject.transform.SetParent(parent, false);

        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return gameObject;
    }

    private static Font LoadReadableFont()
    {
        return UglyDogUIFont.Load();
    }
}
