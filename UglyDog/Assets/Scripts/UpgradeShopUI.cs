using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UpgradeShopUI : MonoBehaviour
{
    public static UpgradeShopUI Instance { get; private set; }
    public static bool BlocksPlayerInput => Instance != null && Instance.IsOpen;

    [System.Serializable]
    private class PanelSettings
    {
        public Vector2 panelSize = new Vector2(1760f, 940f);
        public Color overlayColor = new Color(0.5f, 0.5f, 0.5f, 0.25f);
        public Color panelColor = new Color(0.50f, 0.50f, 0.50f, 0.82f);
    }

    [System.Serializable]
    private class HeaderSettings
    {
        public Vector2 titlePosition = new Vector2(-560f, 404f);
        public Vector2 resourceRowPosition = new Vector2(320f, 404f);
        public Vector2 hintPosition = new Vector2(-560f, 364f);
        public Vector2 closeButtonPosition = new Vector2(834f, 404f);
        public Vector2 resourceItemSize = new Vector2(200f, 76f);
        public Vector2 resourceIconSize = new Vector2(64f, 64f);
        public float resourceSpacing = 36f;
    }

    [System.Serializable]
    private class CardSettings
    {
        public Vector2 viewportSize = new Vector2(1620f, 760f);
        public Vector2 viewportPosition = new Vector2(0f, -10f);
        public Vector2 cardSize = new Vector2(360f, 620f);
        public float cardSpacing = 54f;
        public Color cardColor = new Color(0.24f, 0.24f, 0.24f, 0.94f);
        public Color cardOutlineColor = Color.black;
        public Vector2 iconPosition = new Vector2(0f, 200f);
        public Vector2 iconSize = new Vector2(186f, 186f);
        public Vector2 titlePosition = new Vector2(0f, 58f);
        public Vector2 titleSize = new Vector2(280f, 42f);
        public Vector2 levelPosition = new Vector2(0f, -10f);
        public Vector2 levelSize = new Vector2(220f, 38f);
        public Vector2 descriptionPosition = new Vector2(0f, -140f);
        public Vector2 descriptionSize = new Vector2(260f, 180f);
        public Vector2 buttonPosition = new Vector2(0f, -248f);
        public Vector2 buttonSize = new Vector2(276f, 114f);
    }

    [System.Serializable]
    private class TypographySettings
    {
        public int titleFontSize = 34;
        public int resourceFontSize = 30;
        public int hintFontSize = 16;
        public int cardTitleFontSize = 34;
        public int levelFontSize = 32;
        public int descriptionFontSize = 21;
        public int buttonFontSize = 18;
    }

    [System.Serializable]
    private class ButtonSettings
    {
        public Color enabledColor = new Color(1f, 0.56f, 0.29f, 1f);
        public Color disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        public Vector2 closeButtonSize = new Vector2(52f, 52f);
    }

    private enum ShopCardAction
    {
        UpgradeMoveSpeed,
        UpgradeCollectSpeed
    }

    private enum ResourceDisplayType
    {
        Coin,
        Stone,
        Wood
    }

    private class ShopCardDefinition
    {
        public ShopCardAction action;
        public string title;
        public string iconName;
        public string levelText;
        public string descriptionText;
        public string buttonText;
        public bool interactable;
    }

    private class ShopCardView
    {
        public ShopCardAction action;
        public GameObject root;
        public Image iconImage;
        public Text titleText;
        public Text levelText;
        public Text descriptionText;
        public Button actionButton;
        public Text buttonText;
    }

    private class ResourceView
    {
        public Text valueText;
    }

    [Header("Panel")]
    [SerializeField] private PanelSettings panel = new PanelSettings();

    [Header("Header")]
    [SerializeField] private HeaderSettings header = new HeaderSettings();

    [Header("Cards")]
    [SerializeField] private CardSettings cards = new CardSettings();

    [Header("Typography")]
    [SerializeField] private TypographySettings typography = new TypographySettings();

    [Header("Buttons")]
    [SerializeField] private ButtonSettings buttons = new ButtonSettings();

    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<ResourceDisplayType, ResourceView> resourceViews = new Dictionary<ResourceDisplayType, ResourceView>();
    private readonly List<ShopCardView> cardViews = new List<ShopCardView>();

    private GameObject overlayObject;
    private GameObject panelObject;
    private ScrollRect cardScrollRect;
    private RectTransform cardContentRoot;
    private Font defaultFont;
    private UpgradeShopZone sourceZone;

    public bool IsOpen => panelObject != null && panelObject.activeSelf;

    public static UpgradeShopUI EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        UpgradeShopUI existing = FindObjectOfType<UpgradeShopUI>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject uiObject = new GameObject("Upgrade Shop UI");
        return uiObject.AddComponent<UpgradeShopUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        Close();
    }

    private void OnEnable()
    {
        PlayerUpgradeManager.EnsureInstance().UpgradesChanged += Refresh;
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.ResourcesChanged += Refresh;
        }
    }

    private void OnDisable()
    {
        if (PlayerUpgradeManager.Instance != null)
        {
            PlayerUpgradeManager.Instance.UpgradesChanged -= Refresh;
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.ResourcesChanged -= Refresh;
        }
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        if (Input.GetKeyDown(closeKey))
        {
            Close();
            return;
        }

        Refresh();
    }

    public void Open(UpgradeShopZone zone)
    {
        sourceZone = zone;
        BuildUI();
        panelObject.SetActive(true);
        overlayObject.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Refresh();
    }

    public void Toggle(UpgradeShopZone zone)
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open(zone);
        }
    }

    public void Close()
    {
        sourceZone = null;
        if (panelObject != null)
        {
            panelObject.SetActive(false);
        }

        if (overlayObject != null)
        {
            overlayObject.SetActive(false);
        }
    }

    public void CloseIfOpenedBy(UpgradeShopZone zone)
    {
        if (sourceZone == zone)
        {
            Close();
        }
    }

    private void BuildUI()
    {
        if (panelObject != null)
        {
            return;
        }

        defaultFont = LoadDefaultFont();
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("Upgrade Shop Canvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        overlayObject = CreateOverlay(canvasObject.transform);
        panelObject = CreatePanel(canvasObject.transform);
        CreateHeader(panelObject.transform);
        CreateCardCarousel(panelObject.transform);
    }

    private GameObject CreateOverlay(Transform parent)
    {
        GameObject target = new GameObject("Overlay");
        target.transform.SetParent(parent, false);

        RectTransform rect = target.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = target.AddComponent<Image>();
        image.color = panel.overlayColor;

        Button button = target.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(Close);
        return target;
    }

    private GameObject CreatePanel(Transform parent)
    {
        GameObject target = new GameObject("Panel");
        target.transform.SetParent(parent, false);

        RectTransform rect = target.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = panel.panelSize;

        Image image = target.AddComponent<Image>();
        image.color = panel.panelColor;
        return target;
    }

    private void CreateHeader(Transform parent)
    {
        Text titleText = CreateText(parent, "Title", typography.titleFontSize, FontStyle.Bold, TextAnchor.MiddleLeft, Color.black);
        titleText.text = "DOG SHOP";
        SetRect(titleText.rectTransform, header.titlePosition, new Vector2(520f, 44f));

        CreateResourceRow(parent);

        Text hintText = CreateText(parent, "Hint", typography.hintFontSize, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.16f, 0.16f, 0.16f, 1f));
        hintText.text = "Swipe left or right";
        SetRect(hintText.rectTransform, header.hintPosition, new Vector2(320f, 24f));

        Button closeButton = CreateButton(parent, "X", header.closeButtonPosition, buttons.closeButtonSize);
        closeButton.onClick.AddListener(Close);
    }

    private void CreateResourceRow(Transform parent)
    {
        GameObject row = new GameObject("Resource Row");
        row.transform.SetParent(parent, false);

        RectTransform rowRect = row.AddComponent<RectTransform>();
        SetRect(rowRect, header.resourceRowPosition, new Vector2(740f, 76f));

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = header.resourceSpacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        resourceViews[ResourceDisplayType.Coin] = CreateResourceView(row.transform, "coin1");
        resourceViews[ResourceDisplayType.Stone] = CreateResourceView(row.transform, "stone1");
        resourceViews[ResourceDisplayType.Wood] = CreateResourceView(row.transform, "wood1");
    }

    private ResourceView CreateResourceView(Transform parent, string iconName)
    {
        GameObject item = new GameObject(iconName + " View");
        item.transform.SetParent(parent, false);

        RectTransform itemRect = item.AddComponent<RectTransform>();
        itemRect.sizeDelta = header.resourceItemSize;

        HorizontalLayoutGroup layout = item.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        GameObject iconObject = new GameObject("Icon");
        iconObject.transform.SetParent(item.transform, false);
        Image icon = iconObject.AddComponent<Image>();
        icon.sprite = LoadSprite(iconName);
        icon.preserveAspect = true;
        icon.rectTransform.sizeDelta = header.resourceIconSize;

        Text valueText = CreateText(item.transform, "Value", typography.resourceFontSize, FontStyle.Bold, TextAnchor.MiddleLeft, Color.black);
        valueText.text = "\u6578\u91cf";
        valueText.rectTransform.sizeDelta = new Vector2(120f, 40f);

        return new ResourceView { valueText = valueText };
    }

    private void CreateCardCarousel(Transform parent)
    {
        GameObject viewport = new GameObject("Card Viewport");
        viewport.transform.SetParent(parent, false);

        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.anchoredPosition = cards.viewportPosition;
        viewportRect.sizeDelta = cards.viewportSize;

        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0f);

        viewport.AddComponent<RectMask2D>();

        cardScrollRect = viewport.AddComponent<ScrollRect>();
        cardScrollRect.horizontal = true;
        cardScrollRect.vertical = false;
        cardScrollRect.movementType = ScrollRect.MovementType.Clamped;
        cardScrollRect.scrollSensitivity = 32f;
        cardScrollRect.inertia = true;

        GameObject content = new GameObject("Card Content");
        content.transform.SetParent(viewport.transform, false);

        cardContentRoot = content.AddComponent<RectTransform>();
        cardContentRoot.anchorMin = new Vector2(0f, 0.5f);
        cardContentRoot.anchorMax = new Vector2(0f, 0.5f);
        cardContentRoot.pivot = new Vector2(0f, 0.5f);
        cardContentRoot.anchoredPosition = new Vector2(24f, 0f);
        cardContentRoot.sizeDelta = new Vector2(2400f, cards.cardSize.y + 40f);

        cardScrollRect.viewport = viewportRect;
        cardScrollRect.content = cardContentRoot;
    }

    private void Refresh()
    {
        PlayerUpgradeManager upgrades = PlayerUpgradeManager.EnsureInstance();
        ResourceManager resources = ResourceManager.Instance;

        RefreshResources(resources);

        List<ShopCardDefinition> definitions = BuildCardDefinitions(upgrades, resources);
        EnsureCardViews(definitions);
        BindCardViews(definitions);
        RefreshCardLayout();
    }

    private void RefreshResources(ResourceManager resources)
    {
        int coins = resources != null ? resources.Coins : 0;
        int stone = resources != null ? resources.Stone : 0;
        int wood = resources != null ? resources.Wood : 0;

        SetResourceValue(ResourceDisplayType.Coin, coins);
        SetResourceValue(ResourceDisplayType.Stone, stone);
        SetResourceValue(ResourceDisplayType.Wood, wood);
    }

    private void SetResourceValue(ResourceDisplayType type, int value)
    {
        ResourceView view;
        if (!resourceViews.TryGetValue(type, out view) || view.valueText == null)
        {
            return;
        }

        view.valueText.text = value.ToString();
    }

    private List<ShopCardDefinition> BuildCardDefinitions(PlayerUpgradeManager upgrades, ResourceManager resources)
    {
        List<ShopCardDefinition> definitions = new List<ShopCardDefinition>();

        definitions.Add(BuildMoveSpeedDefinition(upgrades, resources));
        definitions.Add(BuildCollectSpeedDefinition(upgrades, resources));

        return definitions;
    }

    private ShopCardDefinition BuildMoveSpeedDefinition(PlayerUpgradeManager upgrades, ResourceManager resources)
    {
        int level = upgrades.GetLevel(PlayerUpgradeType.MoveSpeed);
        int maxLevel = upgrades.GetMaxLevel(PlayerUpgradeType.MoveSpeed);
        int cost = upgrades.GetNextCost(PlayerUpgradeType.MoveSpeed);
        bool isMax = upgrades.IsMaxLevel(PlayerUpgradeType.MoveSpeed);
        bool canAfford = resources != null && resources.CanSpend(ResourceType.Coin, cost);

        int currentPercent = Mathf.RoundToInt((upgrades.MoveSpeedMultiplier - 1f) * 100f);
        int nextPercent = Mathf.RoundToInt((1f + Mathf.Min(level + 1, maxLevel) * 0.1f - 1f) * 100f);

        return new ShopCardDefinition
        {
            action = ShopCardAction.UpgradeMoveSpeed,
            title = "\u79fb\u52d5\u901f\u5ea6",
            iconName = "runspeedUG",
            levelText = $"Lv.{level}",
            descriptionText = isMax
                ? $"\u8dd1\u901f <color=#89E35B>{currentPercent}%</color>"
                : FormatDeltaLine($"\u8dd1\u901f {currentPercent}%", $"{nextPercent}%"),
            buttonText = isMax ? "\u5df2\u6eff\u7d1a" : $"\u5347\u7d1a\n{cost}\u91d1\u5e63",
            interactable = !isMax && canAfford
        };
    }

    private ShopCardDefinition BuildCollectSpeedDefinition(PlayerUpgradeManager upgrades, ResourceManager resources)
    {
        int level = upgrades.GetCombinedGatherMaxLevel();
        int maxLevel = upgrades.GetMaxLevel(PlayerUpgradeType.WoodGatherSpeed);
        int cost = upgrades.GetCombinedGatherCost();
        bool canBuy = upgrades.CanUpgradeCombinedGather();

        int currentPercent = Mathf.RoundToInt((upgrades.WoodGatherSpeedMultiplier - 1f) * 100f);
        int nextPercent = Mathf.RoundToInt((1f + Mathf.Min(level + 1, maxLevel) * 0.15f - 1f) * 100f);

        return new ShopCardDefinition
        {
            action = ShopCardAction.UpgradeCollectSpeed,
            title = "\u63a1\u96c6\u901f\u5ea6",
            iconName = "collectUG",
            levelText = $"Lv.{level}",
            descriptionText = cost <= 0
                ? $"\u63a1\u96c6 <color=#89E35B>{currentPercent}%</color>"
                : FormatDeltaLine($"\u63a1\u96c6 {currentPercent}%", $"{nextPercent}%"),
            buttonText = cost <= 0 ? "\u5df2\u6eff\u7d1a" : $"\u5347\u7d1a\n{cost}\u91d1\u5e63",
            interactable = cost > 0 && canBuy
        };
    }

    private void EnsureCardViews(List<ShopCardDefinition> definitions)
    {
        while (cardViews.Count < definitions.Count)
        {
            cardViews.Add(CreateCardView(cardViews.Count));
        }

        for (int i = 0; i < cardViews.Count; i++)
        {
            bool shouldShow = i < definitions.Count;
            cardViews[i].root.SetActive(shouldShow);
        }
    }

    private ShopCardView CreateCardView(int index)
    {
        GameObject root = new GameObject("Card " + index);
        root.transform.SetParent(cardContentRoot, false);

        RectTransform rect = root.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = cards.cardSize;
        rect.anchoredPosition = Vector2.zero;

        Image background = root.AddComponent<Image>();
        background.color = cards.cardColor;
        AddOutline(root, cards.cardOutlineColor, new Vector2(5f, -5f));

        GameObject iconObject = new GameObject("Icon");
        iconObject.transform.SetParent(root.transform, false);
        Image iconImage = iconObject.AddComponent<Image>();
        iconImage.preserveAspect = true;
        SetRect(iconImage.rectTransform, cards.iconPosition, cards.iconSize);

        Text titleText = CreateText(root.transform, "Title", typography.cardTitleFontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetRect(titleText.rectTransform, cards.titlePosition, cards.titleSize);

        Text levelText = CreateText(root.transform, "Level", typography.levelFontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetRect(levelText.rectTransform, cards.levelPosition, cards.levelSize);

        Text descriptionText = CreateText(root.transform, "Description", typography.descriptionFontSize, FontStyle.Normal, TextAnchor.UpperCenter, Color.white);
        descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        SetRect(descriptionText.rectTransform, cards.descriptionPosition, cards.descriptionSize);

        Button actionButton = CreateButton(root.transform, "Action", cards.buttonPosition, cards.buttonSize);
        Text buttonText = actionButton.GetComponentInChildren<Text>();

        ShopCardView view = new ShopCardView
        {
            root = root,
            iconImage = iconImage,
            titleText = titleText,
            levelText = levelText,
            descriptionText = descriptionText,
            actionButton = actionButton,
            buttonText = buttonText
        };

        actionButton.onClick.AddListener(() => HandleCardClick(view));
        return view;
    }

    private void BindCardViews(List<ShopCardDefinition> definitions)
    {
        for (int i = 0; i < definitions.Count; i++)
        {
            ShopCardDefinition definition = definitions[i];
            ShopCardView view = cardViews[i];

            view.action = definition.action;
            view.iconImage.sprite = LoadSprite(definition.iconName);
            view.titleText.text = definition.title;
            view.levelText.text = definition.levelText;
            view.descriptionText.text = definition.descriptionText;
            view.buttonText.text = definition.buttonText;
            view.actionButton.interactable = definition.interactable;
            SetButtonColor(view.actionButton, definition.interactable);
        }
    }

    private void RefreshCardLayout()
    {
        if (cardContentRoot == null)
        {
            return;
        }

        int activeCount = 0;
        for (int i = 0; i < cardViews.Count; i++)
        {
            if (!cardViews[i].root.activeSelf)
            {
                continue;
            }

            RectTransform rect = cardViews[i].root.GetComponent<RectTransform>();
            rect.sizeDelta = cards.cardSize;
            rect.anchoredPosition = new Vector2(activeCount * (cards.cardSize.x + cards.cardSpacing), 0f);
            activeCount++;
        }

        float width = Mathf.Max(
            cards.cardSize.x * Mathf.Max(1, activeCount) +
            cards.cardSpacing * Mathf.Max(0, activeCount - 1) +
            48f,
            cards.viewportSize.x);

        cardContentRoot.sizeDelta = new Vector2(width, cards.cardSize.y + 40f);
        Canvas.ForceUpdateCanvases();

        if (cardScrollRect != null)
        {
            cardScrollRect.horizontalNormalizedPosition = 0f;
        }
    }

    private void HandleCardClick(ShopCardView view)
    {
        switch (view.action)
        {
            case ShopCardAction.UpgradeMoveSpeed:
                PlayerUpgradeManager.EnsureInstance().TryUpgrade(PlayerUpgradeType.MoveSpeed);
                break;
            case ShopCardAction.UpgradeCollectSpeed:
                PlayerUpgradeManager.EnsureInstance().TryUpgradeCombinedGather();
                break;
        }

        Refresh();
    }

    private string FormatDeltaLine(string left, string right)
    {
        return $"{left}<color=#FFFFFF> -> </color><color=#89E35B>{right}</color>";
    }

    private Text CreateText(Transform parent, string name, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = defaultFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.supportRichText = true;
        return text;
    }

    private Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size)
    {
        GameObject buttonObject = new GameObject(label + " Button");
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        SetRect(rect, position, size);

        Image image = buttonObject.AddComponent<Image>();
        image.color = buttons.enabledColor;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(buttonObject.transform, label, typography.buttonFontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        text.text = label;
        SetRect(text.rectTransform, Vector2.zero, size);
        return button;
    }

    private void SetButtonColor(Button button, bool enabled)
    {
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = enabled ? buttons.enabledColor : buttons.disabledColor;
        }
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
        DontDestroyOnLoad(eventSystemObject);
    }

    private Font LoadDefaultFont()
    {
        Font font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft JhengHei", "Arial", "Liberation Sans", "Noto Sans CJK TC" },
            18);

        if (font != null)
        {
            return font;
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private Sprite LoadSprite(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
        {
            return null;
        }

        Sprite cached;
        if (spriteCache.TryGetValue(spriteName, out cached))
        {
            return cached;
        }

        string fullPath = Path.Combine(Application.dataPath, "image2D", "UI", spriteName + ".png");
        if (!File.Exists(fullPath))
        {
            spriteCache[spriteName] = null;
            return null;
        }

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
        if (!texture.LoadImage(bytes))
        {
            Destroy(texture);
            spriteCache[spriteName] = null;
            return null;
        }

        texture.name = spriteName;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        sprite.name = spriteName;
        spriteCache[spriteName] = sprite;
        return sprite;
    }

    private void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }
}
