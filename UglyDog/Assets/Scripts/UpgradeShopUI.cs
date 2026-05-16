using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UpgradeShopUI : MonoBehaviour
{
    public static UpgradeShopUI Instance { get; private set; }
    public static bool BlocksPlayerInput => Instance != null && Instance.IsOpen;

    [Header("Input")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Header("Prefab References")]
    [SerializeField] private string closeButtonName = "EscButton";
    [SerializeField] private Button closeButton;
    [SerializeField] private string coinTextName = "CoinNum";
    [SerializeField] private string woodTextName = "WoodNum";
    [SerializeField] private string stoneTextName = "StoneNum";
    [SerializeField] private Text coinText;
    [SerializeField] private Text woodText;
    [SerializeField] private Text stoneText;
    [SerializeField] private UpgradeCard moveSpeedCard = new UpgradeCard { rootName = "MoveSpeedCard" };
    [SerializeField] private UpgradeCard gatherSpeedCard = new UpgradeCard { rootName = "GatherSpeedCard" };

    [Header("Button Colors")]
    [SerializeField] private Color buttonEnabledColor = new Color(1f, 0.56f, 0.29f, 1f);
    [SerializeField] private Color buttonDisabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    private UpgradeShopZone sourceZone;
    private Canvas canvas;

    public bool IsOpen => canvas != null && canvas.enabled && gameObject.activeInHierarchy;

    [Serializable]
    private class UpgradeCard
    {
        public string rootName;
        public Transform root;
        public Text titleText;
        public Text levelText;
        public Text descriptionText;
        public Button upgradeButton;
        public Image upgradeButtonImage;
        public Text upgradeButtonText;

        public bool HasButton => upgradeButton != null;

        public void Resolve(Transform searchRoot)
        {
            if (searchRoot == null)
            {
                return;
            }

            if (root == null && !string.IsNullOrEmpty(rootName))
            {
                root = FindDeepChild(searchRoot, rootName);
            }

            Transform cardRoot = root != null ? root : searchRoot;
            titleText = titleText != null ? titleText : FindComponentInChildrenByName<Text>(cardRoot, "TitleText");
            levelText = levelText != null ? levelText : FindComponentInChildrenByName<Text>(cardRoot, "LevelText");
            descriptionText = descriptionText != null ? descriptionText : FindComponentInChildrenByName<Text>(cardRoot, "DescriptionText");
            upgradeButton = upgradeButton != null ? upgradeButton : FindComponentInChildrenByName<Button>(cardRoot, "UpgradeButton");

            if (upgradeButton != null)
            {
                upgradeButtonImage = upgradeButtonImage != null ? upgradeButtonImage : upgradeButton.GetComponent<Image>();
                upgradeButtonText = upgradeButtonText != null ? upgradeButtonText : FindComponentInChildrenByName<Text>(upgradeButton.transform, "ButtonText");
                upgradeButtonText = upgradeButtonText != null ? upgradeButtonText : upgradeButton.GetComponentInChildren<Text>(true);
            }
        }
    }

    public static UpgradeShopUI EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        UpgradeShopUI existing = FindObjectOfType<UpgradeShopUI>(true);
        if (existing != null)
        {
            Instance = existing;
            existing.Initialize();
            existing.Close();
            return existing;
        }

        GameObject uiObject = new GameObject("Upgrade Shop UI");
        UpgradeShopUI ui = uiObject.AddComponent<UpgradeShopUI>();
        ui.Initialize();
        ui.Close();
        return ui;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Initialize();
        Close();
    }

    private void OnEnable()
    {
        Initialize();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        if (Input.GetKeyDown(closeKey))
        {
            CloseShop();
            return;
        }

        Refresh();
    }

    public void Open(UpgradeShopZone zone)
    {
        sourceZone = zone;
        Initialize();

        if (canvas == null)
        {
            Debug.LogWarning("UpgradeShopUI could not find a Canvas. Drag ShopCanvas.prefab into the scene before using the shop.");
            return;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (canvas != null)
        {
            canvas.enabled = true;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Refresh();
    }

    public void Toggle(UpgradeShopZone zone)
    {
        if (IsOpen)
        {
            CloseShop();
            return;
        }

        Open(zone);
    }

    public void CloseShop()
    {
        Close();
    }

    public void Close()
    {
        sourceZone = null;

        if (canvas != null)
        {
            canvas.enabled = false;
        }

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public void CloseIfOpenedBy(UpgradeShopZone zone)
    {
        if (sourceZone == zone)
        {
            CloseShop();
        }
    }

    public void BuyMoveSpeed()
    {
        PlayerUpgradeManager.EnsureInstance().TryUpgrade(PlayerUpgradeType.MoveSpeed);
        Refresh();
    }

    public void BuyGatherSpeed()
    {
        PlayerUpgradeManager.EnsureInstance().TryUpgradeGatherSpeed();
        Refresh();
    }

    private void Initialize()
    {
        EnsureEventSystem();
        canvas = canvas != null ? canvas : GetComponentInChildren<Canvas>(true);
        EnsureGraphicRaycaster();
        ResolvePrefabReferences();
        WireButtons();
    }

    private void ResolvePrefabReferences()
    {
        closeButton = closeButton != null ? closeButton : FindComponentInChildrenByName<Button>(transform, closeButtonName);
        coinText = coinText != null ? coinText : FindComponentInChildrenByName<Text>(transform, coinTextName);
        woodText = woodText != null ? woodText : FindComponentInChildrenByName<Text>(transform, woodTextName);
        stoneText = stoneText != null ? stoneText : FindComponentInChildrenByName<Text>(transform, stoneTextName);
        moveSpeedCard.Resolve(transform);
        gatherSpeedCard.Resolve(transform);
    }

    private void WireButtons()
    {
        if (closeButton != null)
        {
            closeButton.interactable = true;
            Graphic closeGraphic = closeButton.targetGraphic != null ? closeButton.targetGraphic : closeButton.GetComponent<Graphic>();
            if (closeGraphic != null)
            {
                closeGraphic.raycastTarget = true;
                closeButton.targetGraphic = closeGraphic;
            }

            closeButton.onClick.RemoveListener(CloseShop);
            closeButton.onClick.AddListener(CloseShop);
        }

        if (moveSpeedCard.upgradeButton != null)
        {
            moveSpeedCard.upgradeButton.onClick.RemoveListener(BuyMoveSpeed);
            moveSpeedCard.upgradeButton.onClick.AddListener(BuyMoveSpeed);
        }

        if (gatherSpeedCard.upgradeButton != null)
        {
            gatherSpeedCard.upgradeButton.onClick.RemoveListener(BuyGatherSpeed);
            gatherSpeedCard.upgradeButton.onClick.AddListener(BuyGatherSpeed);
        }

    }

    private void EnsureGraphicRaycaster()
    {
        if (canvas == null)
        {
            return;
        }

        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private void Subscribe()
    {
        PlayerUpgradeManager.EnsureInstance().UpgradesChanged += Refresh;
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.ResourcesChanged += Refresh;
        }
    }

    private void Unsubscribe()
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

    private void Refresh()
    {
        PlayerUpgradeManager upgrades = PlayerUpgradeManager.EnsureInstance();
        ResourceManager resources = ResourceManager.Instance;

        if (coinText != null)
        {
            coinText.text = GetResourceAmount(resources, ResourceType.Coin).ToString();
        }

        if (woodText != null)
        {
            woodText.text = GetResourceAmount(resources, ResourceType.Wood).ToString();
        }

        if (stoneText != null)
        {
            stoneText.text = GetResourceAmount(resources, ResourceType.Stone).ToString();
        }

        RefreshMoveSpeed(upgrades, resources);
        RefreshGatherSpeed(upgrades, resources);
    }

    private void RefreshMoveSpeed(PlayerUpgradeManager upgrades, ResourceManager resources)
    {
        int level = upgrades.GetLevel(PlayerUpgradeType.MoveSpeed);
        int maxLevel = upgrades.GetMaxLevel(PlayerUpgradeType.MoveSpeed);
        int cost = upgrades.GetNextCost(PlayerUpgradeType.MoveSpeed);
        bool isMax = upgrades.IsMaxLevel(PlayerUpgradeType.MoveSpeed);
        bool canAfford = resources != null && resources.CanSpend(ResourceType.Coin, cost);

        RefreshCard(
            moveSpeedCard,
            "\u79fb\u52d5\u901f\u5ea6",
            level,
            maxLevel,
            DescribePercent(level, maxLevel, 10, "\u8dd1\u901f"),
            cost,
            isMax,
            canAfford);
    }

    private void RefreshGatherSpeed(PlayerUpgradeManager upgrades, ResourceManager resources)
    {
        int level = upgrades.GetGatherSpeedLevel();
        int maxLevel = upgrades.GetGatherSpeedMaxLevel();
        int cost = upgrades.GetGatherSpeedNextCost();
        bool isMax = upgrades.IsGatherSpeedMaxLevel();
        bool canAfford = resources != null && resources.CanSpend(ResourceType.Coin, cost);

        RefreshCard(
            gatherSpeedCard,
            "\u63a1\u96c6\u901f\u5ea6",
            level,
            maxLevel,
            DescribePercent(level, maxLevel, 15, "\u63a1\u96c6"),
            cost,
            isMax,
            canAfford);
    }

    private void RefreshCard(
        UpgradeCard card,
        string title,
        int level,
        int maxLevel,
        string description,
        int cost,
        bool isMax,
        bool canAfford,
        string buyTextFormat = "\u5347\u7d1a\n{0}\u91d1\u5e63")
    {
        if (card.titleText != null)
        {
            card.titleText.text = title;
        }

        if (card.levelText != null)
        {
            card.levelText.text = maxLevel > 0 ? $"Lv.{level}" : string.Empty;
        }

        if (card.descriptionText != null)
        {
            card.descriptionText.text = description;
        }

        bool canBuy = !isMax && canAfford;
        if (card.upgradeButton != null)
        {
            card.upgradeButton.interactable = canBuy;
        }

        if (card.upgradeButtonImage != null)
        {
            card.upgradeButtonImage.color = canBuy ? buttonEnabledColor : buttonDisabledColor;
        }

        if (card.upgradeButtonText != null)
        {
            if (isMax)
            {
                card.upgradeButtonText.text = "\u5df2\u6eff\u7d1a";
            }
            else if (!canAfford)
            {
                card.upgradeButtonText.text = "\u91d1\u5e63\u4e0d\u8db3";
            }
            else
            {
                card.upgradeButtonText.text = string.Format(buyTextFormat, cost);
            }
        }
    }

    private string DescribePercent(int level, int maxLevel, int percentPerLevel, string label)
    {
        int currentPercent = level * percentPerLevel;
        if (level >= maxLevel)
        {
            return $"{label} +{currentPercent}%";
        }

        int nextPercent = (level + 1) * percentPerLevel;
        return $"{label} {currentPercent}%\u2192<color=#89E35B>{nextPercent}%</color>";
    }

    private int GetResourceAmount(ResourceManager resources, ResourceType type)
    {
        return resources != null ? resources.GetAmount(type) : 0;
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

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static T FindComponentInChildrenByName<T>(Transform parent, string childName) where T : Component
    {
        Transform child = FindDeepChild(parent, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

}
