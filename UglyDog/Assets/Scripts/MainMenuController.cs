using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    private const int TipsModelRenderQueue = 5000;
    private const string TipsModelLayerName = "Tips 3D Layer";

    [SerializeField] private string gameSceneName = "RoomLobby";
    [SerializeField] private string startButtonName = "Start Button";
    [SerializeField] private string settingsButtonName = "setting Button ";
    [SerializeField] private string exitButtonName = "Exit Button";
    [SerializeField] private string tipsButtonName = "tips Button ";
    [SerializeField] private string tipsPanelName = "TIPS";
    [SerializeField] private GameObject settingsPanelPrefab;
    [SerializeField] private GameObject tipsPanelPrefab;
    [SerializeField] private bool hideTipsOnStart = true;

    [Header("Ambient Audio")]
    [SerializeField] private AudioClip birdAmbienceClip;
    [SerializeField] [Range(0f, 1f)] private float birdAmbienceVolume = 0.45f;
    [SerializeField] private AudioSource birdAmbienceSource;
    [SerializeField] private AudioClip waterAmbienceClip;
    [SerializeField] [Range(0f, 1f)] private float waterAmbienceVolume = 0.35f;
    [SerializeField] private AudioSource waterAmbienceSource;

    [Header("Intro Drop Animation")]
    [SerializeField] private bool playIntroDropAnimation = true;
    [SerializeField] private string logoObjectName = "LOGO";
    [SerializeField] private string menuBackdropName = "Button Backdrop";
    [SerializeField] private float logoDropOffset = 4.5f;
    [SerializeField] private float logoDropDuration = 1f;
    [SerializeField] private float menuDropOffset = 760f;
    [SerializeField] private float menuDropDelay = 0.22f;
    [SerializeField] private float menuDropDuration = 0.95f;
    [SerializeField] private bool playExitSlideAnimation = true;
    [SerializeField] private float exitSlideOffset = 260f;
    [SerializeField] private float exitSlideDelay = 0.35f;
    [SerializeField] private float exitSlideDuration = 0.65f;

    private Button[] menuButtons;
    private SettingsPanelUI settingsPanel;
    private GameObject tipsPanel;

    private void Awake()
    {
        EnsureEventSystem();
        EnsureAudioListener();
        EnsureCanvasCanReceiveClicks();
        DisableDecorativeRaycasts();
        WireMenuButtons();
        EnsureButtonHoverTints();
        EnsureAmbientAudio();
        PrepareSceneTipsPanel();
    }

    private void Start()
    {
        if (playIntroDropAnimation)
        {
            StartCoroutine(PlayIntroDropAnimation());
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenSettings()
    {
        if (settingsPanel == null)
        {
            settingsPanel = CreateSettingsPanel();
        }

        if (settingsPanel != null)
        {
            settingsPanel.Show();
        }
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void WireMenuButtons()
    {
        BindButton(FindButton(startButtonName), StartGame);
        BindButton(FindButton(settingsButtonName), OpenSettings);
        BindButton(FindButton(exitButtonName), ExitGame);
        BindButton(FindButton(tipsButtonName), OpenTips);
        menuButtons = GetComponentsInChildren<Button>(true);
    }

    public void OpenTips()
    {
        if (tipsPanel == null)
        {
            tipsPanel = FindSceneTipsPanel();
        }

        if (tipsPanel == null)
        {
            tipsPanel = CreateTipsPanelFallback();
        }

        if (tipsPanel != null)
        {
            ConfigureTipsPanel(tipsPanel);
            tipsPanel.SetActive(true);
            if (!tipsPanel.activeSelf)
            {
                tipsPanel.SetActive(true);
            }

            Canvas tipsCanvas = tipsPanel.GetComponent<Canvas>();
            if (tipsCanvas != null)
            {
                tipsCanvas.enabled = true;
            }

            tipsPanel.transform.SetAsLastSibling();
        }
    }

    private void PrepareSceneTipsPanel()
    {
        tipsPanel = FindSceneTipsPanel();
        if (tipsPanel == null)
        {
            return;
        }

        ConfigureTipsPanel(tipsPanel);
        if (hideTipsOnStart)
        {
            tipsPanel.SetActive(false);
        }
    }

    private GameObject FindSceneTipsPanel()
    {
        if (string.IsNullOrWhiteSpace(tipsPanelName))
        {
            return null;
        }

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];
            if (candidate == null || candidate.name != tipsPanelName)
            {
                continue;
            }

            if (!candidate.scene.IsValid() || candidate.scene.name == null)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private Button FindButton(string buttonName)
    {
        if (string.IsNullOrWhiteSpace(buttonName))
        {
            return null;
        }

        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == buttonName)
            {
                return button;
            }
        }

        return null;
    }

    private static void BindButton(Button button, UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.enabled = true;
        button.interactable = true;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
            button.targetGraphic = image;
        }
    }

    private void EnsureButtonHoverTints()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.GetComponent<ButtonHoverTint>() == null)
            {
                button.gameObject.AddComponent<ButtonHoverTint>();
            }
        }
    }

    private void EnsureCanvasCanReceiveClicks()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = true;
        }

        GraphicRaycaster raycaster = GetComponentInParent<GraphicRaycaster>();
        if (raycaster == null && canvas != null)
        {
            raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        if (raycaster != null)
        {
            raycaster.enabled = true;
        }
    }

    private void DisableDecorativeRaycasts()
    {
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.GetComponentInParent<Button>() == null)
            {
                graphic.raycastTarget = false;
            }
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private static void EnsureAudioListener()
    {
        if (FindObjectOfType<AudioListener>() != null)
        {
            return;
        }

        Camera camera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        if (camera != null)
        {
            camera.gameObject.AddComponent<AudioListener>();
        }
    }

    private void EnsureAmbientAudio()
    {
        PersistentAmbientAudio.Configure(birdAmbienceClip, birdAmbienceVolume, waterAmbienceClip, waterAmbienceVolume);
        birdAmbienceSource = PersistentAmbientAudio.BirdSource;
        waterAmbienceSource = PersistentAmbientAudio.WaterSource;
    }

    private SettingsPanelUI CreateSettingsPanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }

        if (canvas == null)
        {
            return null;
        }

        if (settingsPanelPrefab == null)
        {
            Debug.LogError("MainMenuController needs Settings Panel(menu).prefab assigned to settingsPanelPrefab.");
            return null;
        }

        GameObject panelObject = Instantiate(settingsPanelPrefab, canvas.transform, false);
        panelObject.name = settingsPanelPrefab.name;

        if (panelObject.transform is RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        SettingsPanelUI panel = panelObject.GetComponent<SettingsPanelUI>();
        if (panel == null)
        {
            Debug.LogError("Settings Panel(menu).prefab must have a SettingsPanelUI component on its root.");
            Destroy(panelObject);
            return null;
        }

        panel.SetExitGameButtonVisible(false);
        panelObject.transform.SetAsLastSibling();
        return panel;
    }

    private GameObject CreateTipsPanelFallback()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }

        if (canvas == null)
        {
            return null;
        }

        if (tipsPanelPrefab == null)
        {
            Debug.LogError("MainMenuController needs TIPS.prefab assigned to tipsPanelPrefab.");
            return null;
        }

        GameObject panelObject = Instantiate(tipsPanelPrefab, canvas.transform, false);
        panelObject.name = tipsPanelPrefab.name;

        if (panelObject.transform is RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        panelObject.transform.SetAsLastSibling();
        ConfigureTipsPanel(panelObject);
        return panelObject;
    }

    private void ConfigureTipsPanel(GameObject panelObject)
    {
        if (panelObject == null)
        {
            return;
        }

        Camera camera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        int parentSortingOrder = parentCanvas != null ? parentCanvas.sortingOrder : 20;

        Canvas tipsCanvas = panelObject.GetComponent<Canvas>();
        if (tipsCanvas != null)
        {
            tipsCanvas.renderMode = camera != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            tipsCanvas.worldCamera = camera;
            tipsCanvas.planeDistance = 0.5f;
            tipsCanvas.overrideSorting = true;
            tipsCanvas.sortingOrder = parentSortingOrder + 50;
            tipsCanvas.enabled = true;
        }

        MatchTipsCanvasScaler(panelObject.GetComponent<CanvasScaler>(), parentCanvas != null ? parentCanvas.GetComponent<CanvasScaler>() : null);

        GraphicRaycaster raycaster = panelObject.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
        {
            raycaster.enabled = true;
        }

        if (panelObject.transform is RectTransform rectTransform)
        {
            rectTransform.localScale = Vector3.one;
        }
        else
        {
            panelObject.transform.localScale = Vector3.one;
        }

        Transform modelLayer = EnsureTipsModelLayer(panelObject.transform);
        HashSet<Transform> reparentedModelRoots = new HashSet<Transform>();
        Renderer[] renderers = panelObject.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Transform modelRoot = FindTipsModelRoot(renderer.transform, panelObject.transform, modelLayer);
            if (modelRoot != null && reparentedModelRoots.Add(modelRoot))
            {
                modelRoot.SetParent(modelLayer, true);
            }

            renderer.enabled = true;
            renderer.sortingOrder = parentSortingOrder + 60;
            ForceTipsRendererOnTop(renderer);
        }
    }

    private static void MatchTipsCanvasScaler(CanvasScaler tipsScaler, CanvasScaler parentScaler)
    {
        if (tipsScaler == null)
        {
            return;
        }

        tipsScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        tipsScaler.referenceResolution = parentScaler != null ? parentScaler.referenceResolution : new Vector2(1920f, 1080f);
        tipsScaler.screenMatchMode = parentScaler != null ? parentScaler.screenMatchMode : CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        tipsScaler.matchWidthOrHeight = parentScaler != null ? parentScaler.matchWidthOrHeight : 0.5f;
        tipsScaler.referencePixelsPerUnit = parentScaler != null ? parentScaler.referencePixelsPerUnit : 100f;
    }

    private static Transform EnsureTipsModelLayer(Transform panelRoot)
    {
        Transform existingLayer = panelRoot.Find(TipsModelLayerName);
        if (existingLayer != null)
        {
            existingLayer.SetAsLastSibling();
            return existingLayer;
        }

        GameObject layerObject = new GameObject(TipsModelLayerName, typeof(RectTransform));
        RectTransform layerRect = layerObject.GetComponent<RectTransform>();
        layerRect.SetParent(panelRoot, false);
        layerRect.anchorMin = Vector2.zero;
        layerRect.anchorMax = Vector2.one;
        layerRect.offsetMin = Vector2.zero;
        layerRect.offsetMax = Vector2.zero;
        layerRect.localRotation = Quaternion.identity;
        layerRect.localScale = Vector3.one;
        layerRect.SetAsLastSibling();
        return layerRect;
    }

    private static Transform FindTipsModelRoot(Transform rendererTransform, Transform panelRoot, Transform modelLayer)
    {
        if (rendererTransform == null || panelRoot == null || rendererTransform.IsChildOf(modelLayer))
        {
            return null;
        }

        Transform current = rendererTransform;
        Transform highestNonUiTransform = null;
        while (current != null && current != panelRoot)
        {
            if (current.GetComponent<RectTransform>() == null)
            {
                highestNonUiTransform = current;
            }

            if (current.parent != null && (current.parent.name == "area1" || current.parent.name == "area2"))
            {
                return current;
            }

            current = current.parent;
        }

        return highestNonUiTransform;
    }

    private static void ForceTipsRendererOnTop(Renderer renderer)
    {
        Material[] materials = renderer.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
            {
                continue;
            }

            material.renderQueue = TipsModelRenderQueue;
            if (material.HasProperty("_ZTest"))
            {
                material.SetInt("_ZTest", (int)CompareFunction.Always);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }
        }
    }

    private IEnumerator PlayIntroDropAnimation()
    {
        Transform logo = FindLogoTransform();
        RectTransform[] menuItems = FindMenuDropTargets();
        RectTransform exitButton = FindRectTransform(exitButtonName);

        Vector3 logoTargetPosition = logo != null ? logo.position : Vector3.zero;
        Vector2[] menuTargetPositions = GetAnchoredPositions(menuItems);
        Vector2 exitTargetPosition = exitButton != null ? exitButton.anchoredPosition : Vector2.zero;

        if (logo != null)
        {
            logo.position = logoTargetPosition + Vector3.up * logoDropOffset;
        }

        for (int i = 0; i < menuItems.Length; i++)
        {
            if (menuItems[i] != null)
            {
                menuItems[i].anchoredPosition = menuTargetPositions[i] + Vector2.up * menuDropOffset;
            }
        }

        if (playExitSlideAnimation && exitButton != null)
        {
            exitButton.anchoredPosition = exitTargetPosition + Vector2.left * exitSlideOffset;
        }

        SetMenuButtonsInteractable(false);

        float elapsed = 0f;
        float totalDuration = Mathf.Max(logoDropDuration, menuDropDelay + menuDropDuration);
        if (playExitSlideAnimation && exitButton != null)
        {
            totalDuration = Mathf.Max(totalDuration, exitSlideDelay + exitSlideDuration);
        }

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (logo != null)
            {
                float logoT = GetDropProgress(elapsed, 0f, logoDropDuration);
                logo.position = Vector3.LerpUnclamped(
                    logoTargetPosition + Vector3.up * logoDropOffset,
                    logoTargetPosition,
                    EaseOutBack(logoT));
            }

            for (int i = 0; i < menuItems.Length; i++)
            {
                if (menuItems[i] == null)
                {
                    continue;
                }

                float menuT = GetDropProgress(elapsed, menuDropDelay, menuDropDuration);
                menuItems[i].anchoredPosition = Vector2.LerpUnclamped(
                    menuTargetPositions[i] + Vector2.up * menuDropOffset,
                    menuTargetPositions[i],
                    EaseOutBack(menuT));
            }

            if (playExitSlideAnimation && exitButton != null)
            {
                float exitT = GetDropProgress(elapsed, exitSlideDelay, exitSlideDuration);
                exitButton.anchoredPosition = Vector2.LerpUnclamped(
                    exitTargetPosition + Vector2.left * exitSlideOffset,
                    exitTargetPosition,
                    EaseOutBack(exitT));
            }

            yield return null;
        }

        if (logo != null)
        {
            logo.position = logoTargetPosition;
        }

        for (int i = 0; i < menuItems.Length; i++)
        {
            if (menuItems[i] != null)
            {
                menuItems[i].anchoredPosition = menuTargetPositions[i];
            }
        }

        if (exitButton != null)
        {
            exitButton.anchoredPosition = exitTargetPosition;
        }

        SetMenuButtonsInteractable(true);
    }

    private Transform FindLogoTransform()
    {
        GameObject logo = GameObject.Find(logoObjectName);
        return logo != null ? logo.transform : null;
    }

    private RectTransform[] FindMenuDropTargets()
    {
        return new[]
        {
            FindRectTransform(menuBackdropName),
            FindRectTransform(startButtonName),
            FindRectTransform(settingsButtonName),
            FindRectTransform(tipsButtonName)
        };
    }

    private RectTransform FindRectTransform(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        foreach (RectTransform rectTransform in GetComponentsInChildren<RectTransform>(true))
        {
            if (rectTransform.name == objectName)
            {
                return rectTransform;
            }
        }

        return null;
    }

    private static Vector2[] GetAnchoredPositions(RectTransform[] rectTransforms)
    {
        Vector2[] positions = new Vector2[rectTransforms.Length];
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            positions[i] = rectTransforms[i] != null ? rectTransforms[i].anchoredPosition : Vector2.zero;
        }

        return positions;
    }

    private void SetMenuButtonsInteractable(bool interactable)
    {
        if (menuButtons == null)
        {
            return;
        }

        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] != null)
            {
                menuButtons[i].interactable = interactable;
            }
        }
    }

    private static float GetDropProgress(float elapsed, float delay, float duration)
    {
        if (duration <= 0f)
        {
            return 1f;
        }

        return Mathf.Clamp01((elapsed - delay) / duration);
    }

    private static float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        float shifted = t - 1f;
        return 1f + c3 * shifted * shifted * shifted + c1 * shifted * shifted;
    }
}
