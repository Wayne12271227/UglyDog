using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuildShopUI : MonoBehaviour
{
    public static BuildShopUI Instance { get; private set; }
    public static bool BlocksPlayerInput => Instance != null && Instance.IsOpen;

    [Header("Input")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Header("Prefab References")]
    [SerializeField] private string closeButtonName = "EscButton";
    [SerializeField] private Button closeButton;
    [SerializeField] private BuildCard towerCard = new BuildCard { rootName = "towerCard", type = BuildSiteBuildingType.ArcherTower };
    [SerializeField] private BuildCard campCard = new BuildCard { rootName = "campCard", type = BuildSiteBuildingType.Barracks };
    [SerializeField] private BuildCard woodCard = new BuildCard { rootName = "woodCard", type = BuildSiteBuildingType.AutoLumber };
    [SerializeField] private BuildCard stoneCard = new BuildCard { rootName = "stoneCard", type = BuildSiteBuildingType.AutoQuarry };

    [Header("Button Colors")]
    [SerializeField] private Color buttonEnabledColor = Color.white;
    [SerializeField] private Color buttonDisabledColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    private ArcherTowerBuildZone sourceZone;
    private Canvas canvas;
    private bool initialized;

    public bool IsOpen => canvas != null && canvas.enabled && gameObject.activeInHierarchy;

    [Serializable]
    private class BuildCard
    {
        public string rootName;
        public BuildSiteBuildingType type;
        public Transform root;
        public Text titleText;
        public Text levelText;
        public Text descriptionText;
        public Button buildButton;
        public Image buildButtonImage;
        public Text buildButtonText;

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
            buildButton = buildButton != null ? buildButton : FindComponentInChildrenByName<Button>(cardRoot, "UpgradeButton");

            if (buildButton != null)
            {
                buildButtonImage = buildButtonImage != null ? buildButtonImage : buildButton.GetComponent<Image>();
                buildButtonText = buildButtonText != null ? buildButtonText : FindComponentInChildrenByName<Text>(buildButton.transform, "ButtonText");
                buildButtonText = buildButtonText != null ? buildButtonText : buildButton.GetComponentInChildren<Text>(true);
            }
        }
    }

    private void Awake()
    {
        Instance = this;
        Initialize();
        Close();
    }

    private void OnEnable()
    {
        Instance = this;
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
            Close();
            return;
        }

        Refresh();
    }

    public void Open(ArcherTowerBuildZone zone)
    {
        sourceZone = zone;
        Instance = this;
        Initialize();

        if (canvas == null)
        {
            Debug.LogWarning("BuildShopUI could not find a Canvas on the build shop prefab.");
            return;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        canvas.enabled = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Refresh();
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

    public void CloseShop()
    {
        Close();
    }

    public void CloseIfOpenedBy(ArcherTowerBuildZone zone)
    {
        if (sourceZone == zone)
        {
            Close();
        }
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        EnsureEventSystem();
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>(true);
        }

        DestroyWrongShopComponent();
        ResolvePrefabReferences();
        WireButtons();
    }

    private void DestroyWrongShopComponent()
    {
        UpgradeShopUI upgradeShop = GetComponent<UpgradeShopUI>();
        if (upgradeShop == null)
        {
            return;
        }

        Destroy(upgradeShop);
    }

    private void ResolvePrefabReferences()
    {
        closeButton = closeButton != null ? closeButton : FindComponentInChildrenByName<Button>(transform, closeButtonName);
        towerCard.Resolve(transform);
        campCard.Resolve(transform);
        woodCard.Resolve(transform);
        stoneCard.Resolve(transform);
    }

    private void WireButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        WireCard(towerCard);
        WireCard(campCard);
        WireCard(woodCard);
        WireCard(stoneCard);
    }

    private void WireCard(BuildCard card)
    {
        if (card == null || card.buildButton == null)
        {
            return;
        }

        card.buildButton.onClick.RemoveAllListeners();
        card.buildButton.onClick.AddListener(() => TryBuild(card));
    }

    private void Subscribe()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.ResourcesChanged -= Refresh;
            ResourceManager.Instance.ResourcesChanged += Refresh;
        }
    }

    private void Unsubscribe()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.ResourcesChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        RefreshCard(towerCard);
        RefreshCard(campCard);
        RefreshCard(woodCard);
        RefreshCard(stoneCard);
    }

    private void RefreshCard(BuildCard card)
    {
        if (card == null)
        {
            return;
        }

        if (card.titleText != null)
        {
            card.titleText.text = ArcherTowerBuildZone.GetDisplayName(card.type);
        }

        if (card.levelText != null)
        {
            card.levelText.text = string.Empty;
        }

        if (card.descriptionText != null)
        {
            card.descriptionText.text = ArcherTowerBuildZone.GetEffectText(card.type);
        }

        bool canAfford = sourceZone != null && sourceZone.CanAfford(card.type);
        bool canBuild = sourceZone != null && !sourceZone.HasCurrentBuilding;

        if (card.buildButton != null)
        {
            card.buildButton.interactable = canBuild;
        }

        if (card.buildButtonImage != null)
        {
            card.buildButtonImage.color = canAfford && canBuild ? buttonEnabledColor : buttonDisabledColor;
        }

        if (card.buildButtonText != null)
        {
            if (!canBuild)
            {
                card.buildButtonText.text = "\u7121\u6cd5\u5efa\u9020";
            }
            else if (canAfford)
            {
                card.buildButtonText.text = "\u5efa\u9020\n" + ArcherTowerBuildZone.GetCostText(card.type);
            }
            else
            {
                string missing = sourceZone != null ? sourceZone.GetMissingCostText(card.type) : ArcherTowerBuildZone.GetCostText(card.type);
                card.buildButtonText.text = "\u7f3a\u5c11\n" + missing;
            }
        }
    }

    private void TryBuild(BuildCard card)
    {
        if (sourceZone == null || card == null)
        {
            return;
        }

        if (sourceZone.TryBeginBuildFromUI(card.type, out string failureMessage))
        {
            Close();
            return;
        }

        if (card.buildButtonText != null && !string.IsNullOrEmpty(failureMessage))
        {
            card.buildButtonText.text = "\u7f3a\u5c11\n" + failureMessage;
        }

        Refresh();
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
