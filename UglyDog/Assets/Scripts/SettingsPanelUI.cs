using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SettingsPanelUI : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const float ValueTextVerticalOffset = -50f;

    public static bool BlocksPlayerInput { get; private set; }

    [SerializeField] private Vector2 panelSize = new Vector2(420f, 640f);
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.28f);
    [SerializeField] private Color panelColor = new Color(0.58f, 0.40f, 0.24f, 0.94f);
    [SerializeField] private Color panelInnerColor = new Color(0.80f, 0.68f, 0.48f, 0.92f);
    [SerializeField] private Color buttonColor = new Color(0.78f, 0.62f, 0.38f, 1f);
    [SerializeField] private Color sliderBackgroundColor = new Color(0.39f, 0.28f, 0.20f, 1f);
    [SerializeField] private Color sliderFillColor = new Color(1f, 0.77f, 0.34f, 1f);
    [SerializeField] private Color sliderHandleColor = new Color(1f, 0.91f, 0.67f, 1f);
    [SerializeField] private bool showExitGameButton;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Text musicValueText;
    [SerializeField] private Text sfxValueText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button exitGameButton;
    [SerializeField] private Text exitGameButtonText;
    [SerializeField] private Button unstuckButton;
    [SerializeField] private Text unstuckButtonText;
    [SerializeField] private bool showUnstuckButton = true;
    [SerializeField] private float exitConfirmSeconds = 2.5f;
    [SerializeField] private float unstuckFeedbackSeconds = 1.2f;

    private RectTransform root;
    private bool isBuilt;
    private bool isRefreshing;
    private bool exitConfirmPending;
    private float exitConfirmDeadline;
    private float unstuckFeedbackDeadline;
    private string exitGameButtonDefaultText = "\u9000\u51fa\u904a\u6232";
    private string unstuckButtonDefaultText = "\u9632\u5361\u7246";
    private Coroutine unstuckFeedbackCoroutine;

    public void Show()
    {
        EnsureBuilt();
        BindExistingControls();
        WireControls();
        RefreshValues();
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        BlocksPlayerInput = true;
    }

    public void Hide()
    {
        ResetExitConfirmation();
        ResetUnstuckButtonText();
        gameObject.SetActive(false);
        BlocksPlayerInput = false;
    }

    public void SetExitGameButtonVisible(bool visible)
    {
        showExitGameButton = visible;
        if (!showExitGameButton)
        {
            ResetExitConfirmation();
        }

        if (exitGameButton != null)
        {
            exitGameButton.gameObject.SetActive(showExitGameButton);
        }
    }

    private void Awake()
    {
        EnsureBuilt();
        RefreshValues();
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        EnsureBuilt();
        BindExistingControls();
        WireControls();
        RefreshValues();
        BlocksPlayerInput = isBuilt;
    }

    private void OnDisable()
    {
        ResetUnstuckButtonText();
        BlocksPlayerInput = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
        }

        if (exitConfirmPending && Time.unscaledTime > exitConfirmDeadline)
        {
            ResetExitConfirmation();
        }

        if (unstuckFeedbackDeadline > 0f && Time.unscaledTime > unstuckFeedbackDeadline)
        {
            ResetUnstuckButtonText();
        }
    }

    private void EnsureBuilt()
    {
        if (isBuilt)
        {
            return;
        }

        root = transform as RectTransform;
        if (root == null)
        {
            return;
        }

        ConfigureRoot();
        BindExistingControls();
        DisableNonUiCloseIcons();

        if (musicSlider == null || sfxSlider == null)
        {
            ClearExistingChildren();
            BuildPanel();
            BindExistingControls();
            DisableNonUiCloseIcons();
        }

        WireControls();
        isBuilt = true;
    }

    private void ConfigureRoot()
    {
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;

        Image overlay = GetComponent<Image>();
        if (overlay == null)
        {
            overlay = gameObject.AddComponent<Image>();
        }

        overlay.color = overlayColor;
        overlay.raycastTarget = true;
    }

    private void BuildPanel()
    {
        Font font = UglyDogUIFont.Load();

        RectTransform panel = CreateRect("Vertical Settings Panel", root, panelSize, Vector2.zero);
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = panelColor;

        RectTransform innerPanel = CreateRect("Inner Panel", panel, new Vector2(panelSize.x - 44f, panelSize.y - 88f), new Vector2(0f, -18f));
        Image innerImage = innerPanel.gameObject.AddComponent<Image>();
        innerImage.color = panelInnerColor;
        innerImage.raycastTarget = false;

        CreateText(panel, font, "\u8a2d\u5b9a", 42, FontStyle.Bold, new Vector2(0f, 250f), new Vector2(220f, 70f), TextAnchor.MiddleCenter);

        closeButton = CreateButton(panel, font, "X", new Vector2(174f, 260f), new Vector2(54f, 54f), null);
        closeButton.name = "Close Button";

        CreateText(panel, font, "\u8072\u97f3", 30, FontStyle.Bold, new Vector2(0f, 120f), new Vector2(300f, 44f), TextAnchor.MiddleCenter);
        musicSlider = CreateSlider(panel, new Vector2(0f, 62f));
        musicValueText = CreateText(panel, font, "100%", 24, FontStyle.Bold, new Vector2(0f, 12f), new Vector2(160f, 34f), TextAnchor.MiddleCenter);

        CreateText(panel, font, "\u97f3\u6548", 30, FontStyle.Bold, new Vector2(0f, -86f), new Vector2(300f, 44f), TextAnchor.MiddleCenter);
        sfxSlider = CreateSlider(panel, new Vector2(0f, -144f));
        sfxValueText = CreateText(panel, font, "100%", 24, FontStyle.Bold, new Vector2(0f, -194f), new Vector2(160f, 34f), TextAnchor.MiddleCenter);

        unstuckButton = CreateButton(panel, font, unstuckButtonDefaultText, new Vector2(0f, -236f), new Vector2(260f, 48f), null);
        unstuckButton.name = "Unstuck Button";
        unstuckButtonText = unstuckButton.GetComponentInChildren<Text>(true);

        exitGameButton = CreateButton(panel, font, "\u9000\u51fa\u904a\u6232", new Vector2(0f, -292f), new Vector2(260f, 48f), null);
        exitGameButton.name = "Exit Game Button";
        exitGameButtonText = exitGameButton.GetComponentInChildren<Text>(true);
        exitGameButton.gameObject.SetActive(showExitGameButton);
    }

    private void ClearExistingChildren()
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private void RefreshValues()
    {
        if (!isBuilt || musicSlider == null || sfxSlider == null)
        {
            return;
        }

        isRefreshing = true;
        EnsureValueTextsVisible();
        musicSlider.value = GameAudioSettings.MusicVolume;
        sfxSlider.value = GameAudioSettings.SfxVolume;
        SetValueText(musicValueText, musicSlider.value);
        SetValueText(sfxValueText, sfxSlider.value);
        isRefreshing = false;
    }

    private void BindExistingControls()
    {
        if (closeButton == null)
        {
            closeButton = FindButtonByNameOrText("Close", "X");
        }

        if (exitGameButton == null)
        {
            exitGameButton = FindButtonByNameOrText("Exit", "\u9000\u51fa\u904a\u6232");
        }

        if (exitGameButton != null && exitGameButtonText == null)
        {
            exitGameButtonText = exitGameButton.GetComponentInChildren<Text>(true);
        }

        if (exitGameButtonText != null && !exitConfirmPending)
        {
            exitGameButtonDefaultText = exitGameButtonText.text;
        }

        if (unstuckButton == null)
        {
            unstuckButton = FindButtonByNameOrText("Unstuck", unstuckButtonDefaultText);
        }

        if (unstuckButton == null)
        {
            CreateUnstuckButtonForExistingPanel();
        }

        if (unstuckButton != null && unstuckButtonText == null)
        {
            unstuckButtonText = unstuckButton.GetComponentInChildren<Text>(true);
        }

        if (unstuckButtonText != null && unstuckFeedbackDeadline <= 0f)
        {
            unstuckButtonText.text = unstuckButtonDefaultText;
        }

        Slider[] sliders = GetComponentsInChildren<Slider>(true);
        if (musicSlider == null && sliders.Length > 0)
        {
            musicSlider = sliders[0];
        }

        if (sfxSlider == null && sliders.Length > 1)
        {
            sfxSlider = sliders[1];
        }

        BindValueTexts();
        EnsureValueTextsVisible();
    }

    private void DisableNonUiCloseIcons()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].name == "iconCross_grey")
            {
                renderers[i].gameObject.SetActive(false);
            }
        }
    }

    private void BindValueTexts()
    {
        if (musicValueText != null && sfxValueText != null)
        {
            return;
        }

        Text[] texts = GetComponentsInChildren<Text>(true);
        List<Text> valueTexts = new List<Text>();
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name.Contains("100%") || texts[i].text.Contains("%"))
            {
                valueTexts.Add(texts[i]);
            }
        }

        valueTexts.Sort((a, b) =>
        {
            RectTransform rectA = a.transform as RectTransform;
            RectTransform rectB = b.transform as RectTransform;
            float yA = rectA != null ? rectA.anchoredPosition.y : 0f;
            float yB = rectB != null ? rectB.anchoredPosition.y : 0f;
            return yB.CompareTo(yA);
        });

        if (musicValueText == null && valueTexts.Count > 0)
        {
            musicValueText = valueTexts[0];
        }

        if (sfxValueText == null && valueTexts.Count > 1)
        {
            sfxValueText = valueTexts[1];
        }
    }

    private void EnsureValueTextsVisible()
    {
        musicValueText = EnsureValueTextVisible(musicValueText, musicSlider, "Music Value Text");
        sfxValueText = EnsureValueTextVisible(sfxValueText, sfxSlider, "Sfx Value Text");
    }

    private Text EnsureValueTextVisible(Text valueText, Slider slider, string objectName)
    {
        if (slider == null)
        {
            return valueText;
        }

        RectTransform sliderRect = slider.transform as RectTransform;
        Transform parent = sliderRect != null && sliderRect.parent != null ? sliderRect.parent : root;
        if (parent == null)
        {
            return valueText;
        }

        if (valueText == null)
        {
            Vector2 position = sliderRect != null
                ? sliderRect.anchoredPosition + new Vector2(0f, ValueTextVerticalOffset)
                : Vector2.zero;

            valueText = CreateText(parent, UglyDogUIFont.Load(), "100%", 26, FontStyle.Bold, position, new Vector2(190f, 40f), TextAnchor.MiddleCenter);
            valueText.gameObject.name = objectName;
        }

        RectTransform textRect = valueText.transform as RectTransform;
        if (textRect != null)
        {
            if (textRect.parent != parent)
            {
                textRect.SetParent(parent, false);
            }

            if (sliderRect != null)
            {
                textRect.anchorMin = sliderRect.anchorMin;
                textRect.anchorMax = sliderRect.anchorMax;
                textRect.pivot = new Vector2(0.5f, 0.5f);
                textRect.anchoredPosition = sliderRect.anchoredPosition + new Vector2(0f, ValueTextVerticalOffset);
            }

            textRect.sizeDelta = new Vector2(190f, 40f);
            textRect.localScale = Vector3.one;
            textRect.SetAsLastSibling();
        }

        valueText.gameObject.SetActive(true);
        valueText.enabled = true;
        valueText.font = UglyDogUIFont.Load();
        valueText.text = string.IsNullOrEmpty(valueText.text) ? "100%" : valueText.text;
        valueText.fontSize = Mathf.Max(valueText.fontSize, 26);
        valueText.fontStyle = FontStyle.Bold;
        valueText.alignment = TextAnchor.MiddleCenter;
        valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        valueText.verticalOverflow = VerticalWrapMode.Overflow;
        valueText.color = Color.white;
        valueText.raycastTarget = false;
        valueText.canvasRenderer.SetAlpha(1f);
        EnsureValueTextOutline(valueText);
        return valueText;
    }

    private static void EnsureValueTextOutline(Text valueText)
    {
        Outline outline = valueText.GetComponent<Outline>();
        if (outline == null)
        {
            outline = valueText.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
    }

    private void WireControls()
    {
        if (closeButton != null)
        {
            EnsureButtonCanReceiveClicks(closeButton);
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }

        if (exitGameButton != null)
        {
            EnsureButtonCanReceiveClicks(exitGameButton);
            exitGameButton.onClick.RemoveListener(ExitGame);
            exitGameButton.onClick.RemoveListener(OnExitGameButtonClicked);
            exitGameButton.onClick.AddListener(OnExitGameButtonClicked);
            exitGameButton.gameObject.SetActive(showExitGameButton);
        }

        if (unstuckButton != null)
        {
            EnsureButtonCanReceiveClicks(unstuckButton);
            unstuckButton.onClick.RemoveListener(OnUnstuckButtonClicked);
            unstuckButton.onClick.AddListener(OnUnstuckButtonClicked);
            unstuckButton.gameObject.SetActive(ShouldShowUnstuckButton());
        }

        if (musicSlider != null)
        {
            musicSlider.minValue = 0f;
            musicSlider.maxValue = 1f;
            ConfigureSliderVisuals(musicSlider);
            musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);
            musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f;
            sfxSlider.maxValue = 1f;
            ConfigureSliderVisuals(sfxSlider);
            sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
        }
    }

    private static void ConfigureSliderVisuals(Slider slider)
    {
        if (slider.fillRect == null)
        {
            slider.fillRect = FindChildRect(slider.transform, "Fill");
        }

        if (slider.handleRect == null)
        {
            slider.handleRect = FindChildRect(slider.transform, "Handle");
        }

        if (slider.targetGraphic == null && slider.handleRect != null)
        {
            Graphic handleGraphic = slider.handleRect.GetComponent<Graphic>();
            if (handleGraphic != null)
            {
                slider.targetGraphic = handleGraphic;
            }
        }

        slider.SetValueWithoutNotify(Mathf.Clamp01(slider.value));
    }

    private static RectTransform FindChildRect(Transform parent, string childName)
    {
        RectTransform[] rectTransforms = parent.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            if (rectTransforms[i].name == childName)
            {
                return rectTransforms[i];
            }
        }

        return null;
    }

    private Button FindButtonByNameOrText(string nameContains, string textValue)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (!string.IsNullOrEmpty(nameContains) && buttons[i].name.Contains(nameContains))
            {
                return buttons[i];
            }
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            Text text = buttons[i].GetComponentInChildren<Text>(true);
            if (text != null && text.text == textValue)
            {
                return buttons[i];
            }
        }

        return null;
    }

    private static void EnsureButtonCanReceiveClicks(Button button)
    {
        button.enabled = true;
        button.interactable = true;

        Graphic targetGraphic = button.targetGraphic;
        if (targetGraphic == null)
        {
            targetGraphic = button.GetComponent<Graphic>();
            button.targetGraphic = targetGraphic;
        }

        if (targetGraphic != null)
        {
            targetGraphic.raycastTarget = true;
        }
    }

    private void OnMusicSliderChanged(float value)
    {
        SetValueText(musicValueText, value);
        if (!isRefreshing)
        {
            GameAudioSettings.SetMusicVolume(value);
        }
    }

    private void OnSfxSliderChanged(float value)
    {
        SetValueText(sfxValueText, value);
        if (!isRefreshing)
        {
            GameAudioSettings.SetSfxVolume(value);
        }
    }

    private void OnExitGameButtonClicked()
    {
        if (!exitConfirmPending || Time.unscaledTime > exitConfirmDeadline)
        {
            exitConfirmPending = true;
            exitConfirmDeadline = Time.unscaledTime + exitConfirmSeconds;
            SetExitButtonText("\u518d\u6309\u4e00\u6b21\u9000\u51fa");
            return;
        }

        ExitGame();
    }

    private void OnUnstuckButtonClicked()
    {
        CatPlayerController player = PreferredPlayerFinder.FindPreferredPlayer();
        if (player == null || !player.TryTeleportToNearbySafeGround())
        {
            SetUnstuckButtonText("\u627e\u4e0d\u5230\u5730\u677f");
            return;
        }

        SetUnstuckButtonText("\u5df2\u79fb\u52d5");
    }

    private void ResetExitConfirmation()
    {
        exitConfirmPending = false;
        SetExitButtonText(exitGameButtonDefaultText);
    }

    private void SetExitButtonText(string value)
    {
        if (exitGameButtonText == null && exitGameButton != null)
        {
            exitGameButtonText = exitGameButton.GetComponentInChildren<Text>(true);
        }

        if (exitGameButtonText != null)
        {
            exitGameButtonText.text = value;
        }
    }

    private void SetUnstuckButtonText(string value)
    {
        if (unstuckButtonText == null && unstuckButton != null)
        {
            unstuckButtonText = unstuckButton.GetComponentInChildren<Text>(true);
        }

        if (unstuckButtonText != null)
        {
            unstuckButtonText.text = value;
            unstuckFeedbackDeadline = Time.unscaledTime + unstuckFeedbackSeconds;
            if (unstuckFeedbackCoroutine != null)
            {
                StopCoroutine(unstuckFeedbackCoroutine);
            }

            unstuckFeedbackCoroutine = StartCoroutine(ResetUnstuckButtonTextAfterDelay());
        }
    }

    private void ResetUnstuckButtonText()
    {
        if (unstuckFeedbackCoroutine != null)
        {
            StopCoroutine(unstuckFeedbackCoroutine);
            unstuckFeedbackCoroutine = null;
        }

        unstuckFeedbackDeadline = 0f;
        if (unstuckButtonText != null)
        {
            unstuckButtonText.text = unstuckButtonDefaultText;
        }
    }

    private IEnumerator ResetUnstuckButtonTextAfterDelay()
    {
        yield return new WaitForSecondsRealtime(unstuckFeedbackSeconds);
        unstuckFeedbackCoroutine = null;
        ResetUnstuckButtonText();
    }

    private async void ExitGame()
    {
        Hide();

        UglyDogRoomLobby lobby = FindObjectOfType<UglyDogRoomLobby>();
        if (lobby != null)
        {
            lobby.LeaveRoom();
            return;
        }

        NetworkRunner[] runners = FindObjectsOfType<NetworkRunner>();
        for (int i = 0; i < runners.Length; i++)
        {
            if (runners[i] != null && runners[i].IsRunning)
            {
                await runners[i].Shutdown();
            }
        }

        SceneManager.LoadScene(MainMenuSceneName);
    }

    private static void SetValueText(Text text, float value)
    {
        if (text != null)
        {
            text.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }

    private Slider CreateSlider(Transform parent, Vector2 position)
    {
        RectTransform sliderRoot = CreateRect("Slider", parent, new Vector2(300f, 42f), position);
        Slider slider = sliderRoot.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        RectTransform background = CreateRect("Background", sliderRoot, new Vector2(300f, 18f), Vector2.zero);
        Image backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = sliderBackgroundColor;

        RectTransform fillArea = CreateRect("Fill Area", sliderRoot, new Vector2(276f, 18f), Vector2.zero);
        RectTransform fill = CreateRect("Fill", fillArea, Vector2.zero, Vector2.zero);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(1f, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = sliderFillColor;

        RectTransform handleArea = CreateRect("Handle Slide Area", sliderRoot, new Vector2(276f, 42f), Vector2.zero);
        RectTransform handle = CreateRect("Handle", handleArea, new Vector2(34f, 34f), Vector2.zero);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = sliderHandleColor;

        slider.targetGraphic = handleImage;
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private void CreateUnstuckButtonForExistingPanel()
    {
        if (root == null || !showUnstuckButton)
        {
            return;
        }

        RectTransform panel = FindChildRect(root, "Vertical Settings Panel");
        if (panel == null)
        {
            panel = root;
        }

        Font font = UglyDogUIFont.Load();
        unstuckButton = CreateButton(panel, font, unstuckButtonDefaultText, new Vector2(0f, -236f), new Vector2(260f, 48f), null);
        unstuckButton.name = "Unstuck Button";
        unstuckButtonText = unstuckButton.GetComponentInChildren<Text>(true);
    }

    private bool ShouldShowUnstuckButton()
    {
        return showUnstuckButton && PreferredPlayerFinder.FindPreferredPlayer() != null;
    }

    private Button CreateButton(Transform parent, Font font, string label, Vector2 position, Vector2 size, UnityAction action)
    {
        RectTransform rect = CreateRect(label + " Button", parent, size, position);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = buttonColor;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (action != null)
        {
            button.onClick.AddListener(action);
        }

        ButtonHoverTint hoverTint = rect.gameObject.AddComponent<ButtonHoverTint>();
        hoverTint.enabled = true;

        CreateText(rect, font, label, 30, FontStyle.Bold, Vector2.zero, size, TextAnchor.MiddleCenter);
        return button;
    }

    private static Text CreateText(Transform parent, Font font, string value, int size, FontStyle style, Vector2 position, Vector2 rectSize, TextAnchor alignment)
    {
        RectTransform rect = CreateRect(value + " Text", parent, rectSize, position);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        rect.localScale = Vector3.one;
        return rect;
    }
}
