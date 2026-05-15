using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UpgradeShopUI : MonoBehaviour
{
    public static UpgradeShopUI Instance { get; private set; }
    public static bool BlocksPlayerInput => Instance != null && Instance.IsOpen;

    [System.Serializable]
    private class UpgradeCardBinding
    {
        public string rootName;
        public GameObject root;
        public Text titleText;
        public Text levelText;
        public Text descriptionText;
        public Button upgradeButton;
        public Image upgradeButtonImage;
        public Text upgradeButtonText;
    }

    [Header("Shop Canvas")]
    [SerializeField] private string shopCanvasName = "ShopCanvas";
    [SerializeField] private GameObject shopCanvas;

    [Header("Close")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;
    [SerializeField] private KeyCode fallbackToggleKey = KeyCode.E;
    [SerializeField] private string closeButtonName = "EscButton";
    [SerializeField] private Button closeButton;

    [Header("Resource Text Names")]
    [SerializeField] private string resourceRootName = "Gameresourse";
    [SerializeField] private string coinTextName = "CoinNum";
    [SerializeField] private string woodTextName = "WoodNum";
    [SerializeField] private string stoneTextName = "StoneNum";

    [Header("Resource Texts")]
    [SerializeField] private Text coinText;
    [SerializeField] private Text woodText;
    [SerializeField] private Text stoneText;

    [Header("Upgrade Cards In ShopCanvas")]
    [SerializeField] private UpgradeCardBinding moveSpeedCard = new UpgradeCardBinding { rootName = "MoveSpeedCard" };
    [SerializeField] private UpgradeCardBinding gatherSpeedCard = new UpgradeCardBinding { rootName = "GatherSpeedCard" };
    [SerializeField] private Color buttonEnabledColor = new Color(1f, 0.56f, 0.29f, 1f);
    [SerializeField] private Color buttonDisabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    private const float MoveSpeedBonusPerLevel = 0.1f;
    private const float GatherSpeedBonusPerLevel = 0.15f;

    private ResourceManager boundResourceManager;
    private PlayerUpgradeManager boundUpgradeManager;
    private UpgradeShopZone sourceZone;
    private bool isOpeningCanvas;

    public bool IsOpen => shopCanvas != null && shopCanvas.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeAfterSceneLoad()
    {
        EnsureInstance();
    }

    public static UpgradeShopUI EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        UpgradeShopUI existing = FindExistingInstance();
        if (existing != null)
        {
            Instance = existing;
            existing.BindShopCanvas();
            return existing;
        }

        GameObject controller = new GameObject("Upgrade Shop UI");
        return controller.AddComponent<UpgradeShopUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (GetComponent<Canvas>() == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        BindShopCanvas();
        if (!isOpeningCanvas)
        {
            Close();
        }
    }

    private void OnEnable()
    {
        BindShopCanvas();
        BindManagers();
        RefreshAll();
    }

    private void OnDisable()
    {
        UnbindManagers();
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

        if (sourceZone == null && Input.GetKeyDown(fallbackToggleKey))
        {
            Close();
        }
    }

    public void Open(UpgradeShopZone zone)
    {
        sourceZone = zone;
        if (!BindShopCanvas())
        {
            return;
        }

        isOpeningCanvas = true;
        shopCanvas.SetActive(true);
        isOpeningCanvas = false;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        BindManagers();
        RefreshAll();
    }

    public void Toggle(UpgradeShopZone zone)
    {
        if (IsOpen)
        {
            Close();
            return;
        }

        Open(zone);
    }

    public void Close()
    {
        sourceZone = null;

        if (shopCanvas != null)
        {
            shopCanvas.SetActive(false);
        }
        else if (GetComponent<Canvas>() != null)
        {
            gameObject.SetActive(false);
        }
    }

    public void CloseShop()
    {
        Close();
    }

    public void CloseIfOpenedBy(UpgradeShopZone zone)
    {
        if (sourceZone == zone)
        {
            Close();
        }
    }

    private bool BindShopCanvas()
    {
        EnsureDefaultCardBindings();

        if (shopCanvas == null)
        {
            Canvas selfCanvas = GetComponent<Canvas>();
            shopCanvas = selfCanvas != null ? selfCanvas.gameObject : FindSceneObject(shopCanvasName);
        }

        if (shopCanvas == null)
        {
            return false;
        }

        BindCloseButton();
        BindResourceTexts();
        BindCard(moveSpeedCard);
        BindCard(gatherSpeedCard);
        EnsureEventSystem();
        return true;
    }

    private void EnsureDefaultCardBindings()
    {
        if (moveSpeedCard == null)
        {
            moveSpeedCard = new UpgradeCardBinding();
        }

        if (gatherSpeedCard == null)
        {
            gatherSpeedCard = new UpgradeCardBinding();
        }

        if (string.IsNullOrEmpty(moveSpeedCard.rootName))
        {
            moveSpeedCard.rootName = "MoveSpeedCard";
        }

        if (string.IsNullOrEmpty(gatherSpeedCard.rootName))
        {
            gatherSpeedCard.rootName = "GatherSpeedCard";
        }
    }

    private void BindCloseButton()
    {
        if (closeButton == null)
        {
            Transform buttonTransform = FindChildRecursive(shopCanvas.transform, closeButtonName);
            if (buttonTransform != null)
            {
                closeButton = buttonTransform.GetComponent<Button>();
            }
        }

        if (closeButton == null)
        {
            return;
        }

        closeButton.onClick.RemoveListener(CloseShop);
        closeButton.onClick.AddListener(CloseShop);
    }

    private void BindResourceTexts()
    {
        Transform root = FindChildRecursive(shopCanvas.transform, resourceRootName);
        if (root == null)
        {
            root = shopCanvas.transform;
        }

        if (coinText == null)
        {
            coinText = FindTextByName(root, coinTextName);
        }

        if (woodText == null)
        {
            woodText = FindTextByName(root, woodTextName);
        }

        if (stoneText == null)
        {
            stoneText = FindTextByName(root, stoneTextName);
        }
    }

    private void BindCard(UpgradeCardBinding card)
    {
        if (card == null || string.IsNullOrEmpty(card.rootName))
        {
            return;
        }

        if (card.root == null)
        {
            Transform root = FindChildRecursive(shopCanvas.transform, card.rootName);
            if (root != null)
            {
                card.root = root.gameObject;
            }
        }

        if (card.root == null)
        {
            return;
        }

        Transform rootTransform = card.root.transform;
        if (card.titleText == null) card.titleText = FindTextByName(rootTransform, "TitleText");
        if (card.levelText == null) card.levelText = FindTextByName(rootTransform, "LevelText");
        if (card.descriptionText == null) card.descriptionText = FindTextByName(rootTransform, "DescriptionText");

        if (card.upgradeButton == null)
        {
            Transform buttonTransform = FindChildRecursive(rootTransform, "UpgradeButton");
            if (buttonTransform != null)
            {
                card.upgradeButton = buttonTransform.GetComponent<Button>();
            }
        }

        if (card.upgradeButton != null)
        {
            if (card.upgradeButtonImage == null)
            {
                card.upgradeButtonImage = card.upgradeButton.GetComponent<Image>();
            }

            if (card.upgradeButtonText == null)
            {
                card.upgradeButtonText = card.upgradeButton.GetComponentInChildren<Text>();
            }
        }
    }

    private void BindManagers()
    {
        BindResourceManager();
        BindUpgradeManager();
    }

    private void BindResourceManager()
    {
        ResourceManager current = ResourceManager.Instance;
        if (boundResourceManager == current)
        {
            return;
        }

        if (boundResourceManager != null)
        {
            boundResourceManager.ResourcesChanged -= RefreshAll;
        }

        boundResourceManager = current;
        if (boundResourceManager != null)
        {
            boundResourceManager.ResourcesChanged += RefreshAll;
        }
    }

    private void BindUpgradeManager()
    {
        PlayerUpgradeManager current = PlayerUpgradeManager.EnsureInstance();
        if (boundUpgradeManager == current)
        {
            return;
        }

        if (boundUpgradeManager != null)
        {
            boundUpgradeManager.UpgradesChanged -= RefreshAll;
        }

        boundUpgradeManager = current;
        if (boundUpgradeManager != null)
        {
            boundUpgradeManager.UpgradesChanged += RefreshAll;
        }
    }

    private void UnbindManagers()
    {
        if (boundResourceManager != null)
        {
            boundResourceManager.ResourcesChanged -= RefreshAll;
            boundResourceManager = null;
        }

        if (boundUpgradeManager != null)
        {
            boundUpgradeManager.UpgradesChanged -= RefreshAll;
            boundUpgradeManager = null;
        }
    }

    private void RefreshAll()
    {
        BindShopCanvas();
        RefreshResources();
        RefreshUpgradeCards();
    }

    private void RefreshResources()
    {
        ResourceManager resources = ResourceManager.Instance;
        if (resources == null)
        {
            SetText(coinText, 0);
            SetText(woodText, 0);
            SetText(stoneText, 0);
            return;
        }

        SetText(coinText, resources.Coins);
        SetText(woodText, resources.Wood);
        SetText(stoneText, resources.Stone);
    }

    private void RefreshUpgradeCards()
    {
        PlayerUpgradeManager upgrades = PlayerUpgradeManager.EnsureInstance();
        ResourceManager resources = ResourceManager.Instance;

        RefreshMoveSpeedCard(upgrades, resources);
        RefreshGatherSpeedCard(upgrades, resources);
    }

    private void RefreshMoveSpeedCard(PlayerUpgradeManager upgrades, ResourceManager resources)
    {
        int level = upgrades.GetLevel(PlayerUpgradeType.MoveSpeed);
        int maxLevel = upgrades.GetMaxLevel(PlayerUpgradeType.MoveSpeed);
        int cost = upgrades.GetNextCost(PlayerUpgradeType.MoveSpeed);
        bool isMaxLevel = upgrades.IsMaxLevel(PlayerUpgradeType.MoveSpeed);
        bool canBuy = !isMaxLevel && resources != null && resources.CanSpend(ResourceType.Coin, cost);
        int currentBonus = GetBonusPercent(level, MoveSpeedBonusPerLevel);
        int nextBonus = GetBonusPercent(Mathf.Min(level + 1, maxLevel), MoveSpeedBonusPerLevel);

        SetCard(
            moveSpeedCard,
            "移動速度",
            level,
            isMaxLevel ? $"跑速 <color=#89E35B>{currentBonus}%</color>" : $"跑速 {currentBonus}%→<color=#89E35B>{nextBonus}%</color>",
            isMaxLevel ? "已滿級" : $"升級\n{cost}金幣",
            canBuy,
            TryUpgradeMoveSpeed);
    }

    private void RefreshGatherSpeedCard(PlayerUpgradeManager upgrades, ResourceManager resources)
    {
        int level = upgrades.GetCombinedGatherMaxLevel();
        int maxLevel = upgrades.GetMaxLevel(PlayerUpgradeType.WoodGatherSpeed);
        int cost = upgrades.GetCombinedGatherCost();
        bool isMaxLevel = cost <= 0;
        bool canBuy = !isMaxLevel && resources != null && resources.CanSpend(ResourceType.Coin, cost);
        int currentBonus = GetBonusPercent(level, GatherSpeedBonusPerLevel);
        int nextBonus = GetBonusPercent(Mathf.Min(level + 1, maxLevel), GatherSpeedBonusPerLevel);

        SetCard(
            gatherSpeedCard,
            "採集速度",
            level,
            isMaxLevel ? $"採集 <color=#89E35B>{currentBonus}%</color>" : $"採集 {currentBonus}%→<color=#89E35B>{nextBonus}%</color>",
            isMaxLevel ? "已滿級" : $"升級\n{cost}金幣",
            canBuy,
            TryUpgradeGatherSpeed);
    }

    private void SetCard(
        UpgradeCardBinding card,
        string title,
        int level,
        string description,
        string buttonText,
        bool canBuy,
        UnityEngine.Events.UnityAction clickAction)
    {
        if (card == null || card.root == null)
        {
            return;
        }

        SetText(card.titleText, title);
        SetText(card.levelText, $"Lv.{level}");
        SetText(card.descriptionText, description);
        SetText(card.upgradeButtonText, buttonText);

        if (card.upgradeButton != null)
        {
            card.upgradeButton.onClick.RemoveAllListeners();
            card.upgradeButton.onClick.AddListener(clickAction);
            card.upgradeButton.interactable = canBuy;
        }

        if (card.upgradeButtonImage != null)
        {
            card.upgradeButtonImage.color = canBuy ? buttonEnabledColor : buttonDisabledColor;
        }
    }

    private void TryUpgradeMoveSpeed()
    {
        PlayerUpgradeManager.EnsureInstance().TryUpgrade(PlayerUpgradeType.MoveSpeed);
        RefreshAll();
    }

    private void TryUpgradeGatherSpeed()
    {
        PlayerUpgradeManager.EnsureInstance().TryUpgradeCombinedGather();
        RefreshAll();
    }

    private static int GetBonusPercent(int level, float bonusPerLevel)
    {
        return Mathf.RoundToInt(level * bonusPerLevel * 100f);
    }

    private static void SetText(Text text, int value)
    {
        if (text != null)
        {
            text.text = value.ToString();
        }
    }

    private static void SetText(Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private static GameObject FindSceneObject(string objectName)
    {
        GameObject exact = GameObject.Find(objectName);
        if (exact != null)
        {
            return exact;
        }

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];
            if (candidate.name == objectName && candidate.scene.IsValid())
            {
                return candidate;
            }
        }

        return null;
    }

    private static UpgradeShopUI FindExistingInstance()
    {
        UpgradeShopUI active = FindObjectOfType<UpgradeShopUI>();
        if (active != null)
        {
            return active;
        }

        UpgradeShopUI[] allObjects = Resources.FindObjectsOfTypeAll<UpgradeShopUI>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            UpgradeShopUI candidate = allObjects[i];
            if (candidate != null && candidate.gameObject.scene.IsValid())
            {
                return candidate;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static Text FindTextByName(Transform root, string textName)
    {
        Transform textTransform = FindChildRecursive(root, textName);
        return textTransform != null ? textTransform.GetComponent<Text>() : null;
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
}
