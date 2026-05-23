using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "RoomLobby";
    [SerializeField] private string startButtonName = "Start Button";
    [SerializeField] private string settingsButtonName = "setting Button ";
    [SerializeField] private string exitButtonName = "Exit Button";
    [SerializeField] private GameObject settingsPanelPrefab;

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

    private Button[] menuButtons;
    private SettingsPanelUI settingsPanel;

    private void Awake()
    {
        EnsureEventSystem();
        EnsureAudioListener();
        EnsureCanvasCanReceiveClicks();
        DisableDecorativeRaycasts();
        WireMenuButtons();
        EnsureButtonHoverTints();
        EnsureAmbientAudio();
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
        menuButtons = GetComponentsInChildren<Button>(true);
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

    private IEnumerator PlayIntroDropAnimation()
    {
        Transform logo = FindLogoTransform();
        RectTransform[] menuItems = FindMenuDropTargets();

        Vector3 logoTargetPosition = logo != null ? logo.position : Vector3.zero;
        Vector2[] menuTargetPositions = GetAnchoredPositions(menuItems);

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

        SetMenuButtonsInteractable(false);

        float elapsed = 0f;
        float totalDuration = Mathf.Max(logoDropDuration, menuDropDelay + menuDropDuration);
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
            FindRectTransform(exitButtonName)
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
