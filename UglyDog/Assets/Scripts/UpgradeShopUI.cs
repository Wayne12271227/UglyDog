using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UpgradeShopUI : MonoBehaviour
{
    public static UpgradeShopUI Instance { get; private set; }
    public static bool BlocksPlayerInput => Instance != null && Instance.IsOpen;

    [SerializeField] private Vector2 panelSize = new Vector2(660f, 440f);
    [SerializeField] private Color panelColor = new Color(0.08f, 0.07f, 0.055f, 0.94f);
    [SerializeField] private Color rowColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color buttonColor = new Color(0.95f, 0.66f, 0.18f, 1f);
    [SerializeField] private Color disabledButtonColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    private readonly Dictionary<PlayerUpgradeType, UpgradeRow> rows = new Dictionary<PlayerUpgradeType, UpgradeRow>();
    private Canvas canvas;
    private GameObject panel;
    private Text resourceText;
    private Font defaultFont;
    private UpgradeShopZone sourceZone;

    public bool IsOpen => panel != null && panel.activeSelf;

    private class UpgradeRow
    {
        public Text nameText;
        public Text levelText;
        public Text effectText;
        public Text costText;
        public Button buyButton;
        public Text buttonText;
    }

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
        panel.SetActive(true);
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
        if (panel != null)
        {
            panel.SetActive(false);
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
        if (panel != null)
        {
            return;
        }

        defaultFont = LoadDefaultFont();
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("Upgrade Shop Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        panel = CreatePanel(canvasObject.transform);
        CreateTitle(panel.transform);
        resourceText = CreateText(panel.transform, "Resources", 18, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(resourceText.rectTransform, new Vector2(0f, 125f), new Vector2(560f, 34f));

        CreateUpgradeRow(PlayerUpgradeType.MoveSpeed, 50f);
        CreateUpgradeRow(PlayerUpgradeType.WoodGatherSpeed, -60f);
        CreateUpgradeRow(PlayerUpgradeType.StoneGatherSpeed, -170f);
    }

    private GameObject CreatePanel(Transform parent)
    {
        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = panelSize;

        Image image = panelObject.AddComponent<Image>();
        image.color = panelColor;
        return panelObject;
    }

    private void CreateTitle(Transform parent)
    {
        Text title = CreateText(parent, "商店", 34, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0f, 180f), new Vector2(520f, 48f));

        Button closeButton = CreateButton(parent, "X", new Vector2(292f, 180f), new Vector2(44f, 44f));
        closeButton.onClick.AddListener(Close);
    }

    private void CreateUpgradeRow(PlayerUpgradeType type, float y)
    {
        GameObject rowObject = new GameObject(type.ToString() + " Row");
        rowObject.transform.SetParent(panel.transform, false);

        RectTransform rowRect = rowObject.AddComponent<RectTransform>();
        SetRect(rowRect, new Vector2(0f, y), new Vector2(580f, 88f));

        Image rowImage = rowObject.AddComponent<Image>();
        rowImage.color = rowColor;

        UpgradeRow row = new UpgradeRow();
        row.nameText = CreateText(rowObject.transform, "Name", 20, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(row.nameText.rectTransform, new Vector2(-200f, 20f), new Vector2(170f, 30f));

        row.levelText = CreateText(rowObject.transform, "Level", 16, FontStyle.Normal, TextAnchor.MiddleLeft);
        SetRect(row.levelText.rectTransform, new Vector2(-200f, -18f), new Vector2(170f, 28f));

        row.effectText = CreateText(rowObject.transform, "Effect", 16, FontStyle.Normal, TextAnchor.MiddleLeft);
        SetRect(row.effectText.rectTransform, new Vector2(45f, 20f), new Vector2(280f, 30f));

        row.costText = CreateText(rowObject.transform, "Cost", 16, FontStyle.Normal, TextAnchor.MiddleLeft);
        SetRect(row.costText.rectTransform, new Vector2(45f, -18f), new Vector2(280f, 28f));

        row.buyButton = CreateButton(rowObject.transform, "升級", new Vector2(235f, 0f), new Vector2(92f, 46f));
        row.buttonText = row.buyButton.GetComponentInChildren<Text>();
        row.buyButton.onClick.AddListener(() => Buy(type));

        rows[type] = row;
    }

    private Text CreateText(Transform parent, string name, int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = defaultFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Font LoadDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            return font;
        }

        return Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft JhengHei", "Arial", "Liberation Sans", "Noto Sans CJK TC" },
            16);
    }

    private Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size)
    {
        GameObject buttonObject = new GameObject(label + " Button");
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        SetRect(rect, position, size);

        Image image = buttonObject.AddComponent<Image>();
        image.color = buttonColor;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(buttonObject.transform, label, 17, FontStyle.Bold, TextAnchor.MiddleCenter);
        text.text = label;
        SetRect(text.rectTransform, Vector2.zero, size);
        return button;
    }

    private void Refresh()
    {
        PlayerUpgradeManager upgrades = PlayerUpgradeManager.EnsureInstance();
        ResourceManager resources = ResourceManager.Instance;

        if (resourceText != null)
        {
            int coins = resources != null ? resources.Coins : 0;
            int wood = resources != null ? resources.Wood : 0;
            int stone = resources != null ? resources.Stone : 0;
            resourceText.text = $"金幣 {coins}    木頭 {wood}    石頭 {stone}";
        }

        foreach (KeyValuePair<PlayerUpgradeType, UpgradeRow> pair in rows)
        {
            RefreshRow(upgrades, resources, pair.Key, pair.Value);
        }
    }

    private void RefreshRow(PlayerUpgradeManager upgrades, ResourceManager resources, PlayerUpgradeType type, UpgradeRow row)
    {
        int level = upgrades.GetLevel(type);
        int maxLevel = upgrades.GetMaxLevel(type);
        bool isMaxLevel = upgrades.IsMaxLevel(type);
        int cost = upgrades.GetNextCost(type);
        bool canAfford = resources != null && resources.CanSpend(ResourceType.Coin, cost);
        bool canBuy = !isMaxLevel && canAfford;

        row.nameText.text = upgrades.GetDisplayName(type);
        row.levelText.text = $"Lv.{level} / {maxLevel}";
        row.effectText.text = upgrades.GetEffectText(type);
        row.costText.text = isMaxLevel ? "已滿級" : $"價格 {cost} 金幣";

        row.buyButton.interactable = canBuy;
        row.buttonText.text = isMaxLevel ? "完成" : canAfford ? "升級" : "不足";

        Image buttonImage = row.buyButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = canBuy ? buttonColor : disabledButtonColor;
        }
    }

    private void Buy(PlayerUpgradeType type)
    {
        PlayerUpgradeManager.EnsureInstance().TryUpgrade(type);
        Refresh();
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

    private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }
}
