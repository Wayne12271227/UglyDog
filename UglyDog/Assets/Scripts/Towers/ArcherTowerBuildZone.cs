using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum BuildSiteBuildingType
{
    ArcherTower,
    AutoLumber,
    AutoQuarry
}

[RequireComponent(typeof(Collider))]
public class ArcherTowerBuildZone : MonoBehaviour
{
    [Header("Build Site")]
    [SerializeField] private KeyCode openKey = KeyCode.E;
    [SerializeField] private float buildDuration = 3f;
    [SerializeField] private int buildingHealth = 60;
    [SerializeField] private Transform buildAnchor;
    [SerializeField] private Vector3 buildLocalOffset = Vector3.zero;

    [Header("Archer Tower")]
    [SerializeField] private float towerAttackRange = 8f;
    [SerializeField] private float towerShotsPerSecond = 1f;
    [SerializeField] private int towerDamage = 5;
    [SerializeField] private float towerProjectileSpeed = 12f;

    [Header("Prepared Dashed Range")]
    [SerializeField] private bool autoFindDashedRangeRenderers = true;
    [SerializeField] private Renderer[] dashedRangeRenderers;
    [SerializeField] private Color neutralColor = Color.white;
    [SerializeField] private Color dogColor = new Color(1f, 0.682f, 0f, 1f);
    [SerializeField] private Color catColor = new Color(0f, 0.847f, 1f, 1f);

    [Header("Detection")]
    [SerializeField] private LayerMask detectionLayers = ~0;
    [SerializeField] private Vector3 promptLocalOffset = new Vector3(0f, 2f, 0f);

    private Collider zoneCollider;
    private CatPlayerController activeBuilder;
    private GameObject currentBuilding;
    private TeamBuilding currentTeamBuilding;
    private BuildSiteBuildingType pendingType;
    private bool isBuilding;
    private float buildProgress;
    private GameObject promptObject;
    private TextMesh promptText;
    private BuildSiteUI ui;
    private MaterialPropertyBlock dashedRangePropertyBlock;
    private MinionTeam ownerTeam;
    private bool hasOwner;

    private void Reset()
    {
        zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;
        ApplyOwnershipColor();
    }

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;
        CacheDashedRangeRenderersIfNeeded();
        ApplyOwnershipColor();
    }

    private void OnValidate()
    {
        buildDuration = Mathf.Max(0.1f, buildDuration);
        buildingHealth = Mathf.Max(1, buildingHealth);
    }

    private void Update()
    {
        CatPlayerController builder = activeBuilder != null && IsPlayerInsideZone(activeBuilder)
            ? activeBuilder
            : FindPlayerInsideZone();
        if (builder == null)
        {
            activeBuilder = null;
            HidePrompt();
            HideBuildUI();
            if (isBuilding)
            {
                CancelBuild();
            }

            return;
        }

        activeBuilder = builder;

        if (isBuilding)
        {
            UpdateBuild(builder);
            return;
        }

        ShowPrompt(builder);
        if (Input.GetKeyDown(openKey))
        {
            TryOpenBuildUI(builder);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CatPlayerController player = GetPlayer(other);
        if (player == null)
        {
            return;
        }

        activeBuilder = player;
        ShowPrompt(player);
    }

    private void OnTriggerExit(Collider other)
    {
        CatPlayerController player = GetPlayer(other);
        if (player == null || player != activeBuilder)
        {
            return;
        }

        if (isBuilding)
        {
            CancelBuild();
        }

        activeBuilder = null;
        HidePrompt();
        HideBuildUI();
    }

    public void BeginBuild(BuildSiteBuildingType type)
    {
        if (activeBuilder == null || currentBuilding != null)
        {
            return;
        }

        if (!CanAfford(type))
        {
            FlashPrompt("\u8cc7\u6e90\u4e0d\u8db3");
            return;
        }

        pendingType = type;
        buildProgress = 0f;
        isBuilding = true;
        HideBuildUI();
        FlashPrompt("\u5efa\u9020\u4e2d...");
    }

    private void UpdateBuild(CatPlayerController builder)
    {
        if (builder == null)
        {
            CancelBuild();
            return;
        }

        builder.PlayBuild();
        buildProgress += Time.deltaTime;
        FlashPrompt("\u5efa\u9020\u4e2d " + Mathf.CeilToInt(Mathf.Max(0f, buildDuration - buildProgress)) + "s");

        if (buildProgress < buildDuration)
        {
            return;
        }

        CompleteBuild(builder);
    }

    private void CompleteBuild(CatPlayerController builder)
    {
        if (!SpendCost(pendingType))
        {
            CancelBuild();
            FlashPrompt("\u8cc7\u6e90\u4e0d\u8db3");
            return;
        }

        MinionTeam team = GetPlayerTeam(builder);
        CreateBuilding(pendingType, team);
        isBuilding = false;
        buildProgress = 0f;
        builder.StopAction();
        HidePrompt();
    }

    private void CancelBuild()
    {
        if (activeBuilder != null)
        {
            activeBuilder.StopAction();
        }

        isBuilding = false;
        buildProgress = 0f;
    }

    private void TryOpenBuildUI(CatPlayerController builder)
    {
        if (currentBuilding != null)
        {
            FlashPrompt("\u9700\u5148\u6467\u6bc0\u73fe\u6709\u5efa\u7bc9");
            return;
        }

        EnsureBuildUI();
        ui.Open(this, builder);
    }

    private void HideBuildUI()
    {
        if (ui != null)
        {
            ui.Close();
        }
    }

    private void EnsureBuildUI()
    {
        if (ui != null)
        {
            return;
        }

        GameObject uiObject = new GameObject("Build Site UI");
        ui = uiObject.AddComponent<BuildSiteUI>();
    }

    private void CreateBuilding(BuildSiteBuildingType type, MinionTeam team)
    {
        GameObject buildingObject = new GameObject(team + " " + GetDisplayName(type));
        buildingObject.transform.position = GetBuildPosition();
        buildingObject.transform.rotation = transform.rotation;

        BuildingHealth health = buildingObject.AddComponent<BuildingHealth>();
        health.Configure(buildingHealth);
        health.Destroyed += OnBuildingDestroyed;

        currentBuilding = buildingObject;
        currentTeamBuilding = buildingObject.AddComponent<TeamBuilding>();
        currentTeamBuilding.Configure(team);

        BoxCollider collider = buildingObject.AddComponent<BoxCollider>();
        collider.size = GetFootprint(type);
        collider.center = new Vector3(0f, collider.size.y * 0.5f, 0f);

        if (type == BuildSiteBuildingType.ArcherTower)
        {
            ArcherTower tower = buildingObject.AddComponent<ArcherTower>();
            tower.Configure(team, towerAttackRange, towerShotsPerSecond, towerDamage, towerProjectileSpeed);
        }
        else
        {
            AutoResourceBuilding producer = buildingObject.AddComponent<AutoResourceBuilding>();
            producer.Configure(type == BuildSiteBuildingType.AutoQuarry ? ResourceType.Stone : ResourceType.Wood, 1, 5f);
            CreateWhiteResourceBuildingModel(buildingObject.transform, type);
        }

        SetOwner(team);
    }

    private void OnBuildingDestroyed(BuildingHealth health)
    {
        if (health != null)
        {
            health.Destroyed -= OnBuildingDestroyed;
        }

        currentBuilding = null;
        currentTeamBuilding = null;
        hasOwner = false;
        ApplyOwnershipColor();
    }

    private Vector3 GetBuildPosition()
    {
        return buildAnchor != null ? buildAnchor.position : transform.TransformPoint(buildLocalOffset);
    }

    private void SetOwner(MinionTeam team)
    {
        ownerTeam = team;
        hasOwner = true;
        ApplyOwnershipColor();
    }

    public bool CanAfford(BuildSiteBuildingType type)
    {
        ResourceManager resources = ResourceManager.Instance;
        return resources != null && resources.CanSpend(ResourceType.Coin, GetCoinCost(type))
            && resources.CanSpend(ResourceType.Wood, GetWoodCost(type))
            && resources.CanSpend(ResourceType.Stone, GetStoneCost(type));
    }

    private bool SpendCost(BuildSiteBuildingType type)
    {
        if (!CanAfford(type))
        {
            return false;
        }

        ResourceManager resources = ResourceManager.Instance;
        resources.Spend(ResourceType.Coin, GetCoinCost(type));
        resources.Spend(ResourceType.Wood, GetWoodCost(type));
        resources.Spend(ResourceType.Stone, GetStoneCost(type));
        return true;
    }

    public static string GetDisplayName(BuildSiteBuildingType type)
    {
        switch (type)
        {
            case BuildSiteBuildingType.ArcherTower:
                return "\u5f13\u7bad\u5854";
            case BuildSiteBuildingType.AutoLumber:
                return "\u81ea\u52d5\u4f10\u6728\u6a5f";
            case BuildSiteBuildingType.AutoQuarry:
                return "\u81ea\u52d5\u63a1\u77f3\u6a5f";
            default:
                return "\u5efa\u7bc9";
        }
    }

    public static string GetEffectText(BuildSiteBuildingType type)
    {
        switch (type)
        {
            case BuildSiteBuildingType.ArcherTower:
                return "\u653b\u64ca\u8def\u5f91\u4e0a\u7684\u6575\u65b9\u58eb\u5175";
            case BuildSiteBuildingType.AutoLumber:
                return "\u6bcf 5 \u79d2 +1 \u6728\u982d";
            case BuildSiteBuildingType.AutoQuarry:
                return "\u6bcf 5 \u79d2 +1 \u77f3\u982d";
            default:
                return string.Empty;
        }
    }

    public static int GetCoinCost(BuildSiteBuildingType type)
    {
        return type == BuildSiteBuildingType.ArcherTower ? 100 : 50;
    }

    public static int GetWoodCost(BuildSiteBuildingType type)
    {
        return type == BuildSiteBuildingType.AutoLumber ? 25 : 0;
    }

    public static int GetStoneCost(BuildSiteBuildingType type)
    {
        return type == BuildSiteBuildingType.AutoQuarry ? 25 : 0;
    }

    private static Vector3 GetFootprint(BuildSiteBuildingType type)
    {
        return type == BuildSiteBuildingType.ArcherTower
            ? new Vector3(1.6f, 2.6f, 1.6f)
            : new Vector3(2.2f, 1.8f, 2.2f);
    }

    private CatPlayerController GetPlayer(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        CatPlayerController player = other.GetComponentInParent<CatPlayerController>();
        return player != null && player.enabled && player.gameObject.activeInHierarchy ? player : null;
    }

    private CatPlayerController FindPlayerInsideZone()
    {
        if (zoneCollider == null)
        {
            zoneCollider = GetComponent<Collider>();
        }

        Bounds bounds = zoneCollider.bounds;
        Collider[] hits = Physics.OverlapBox(bounds.center, bounds.extents, transform.rotation, detectionLayers, QueryTriggerInteraction.Collide);
        foreach (Collider hit in hits)
        {
            if (hit == zoneCollider)
            {
                continue;
            }

            CatPlayerController player = GetPlayer(hit);
            if (player != null)
            {
                return player;
            }
        }

        return null;
    }

    private bool IsPlayerInsideZone(CatPlayerController player)
    {
        if (player == null)
        {
            return false;
        }

        if (zoneCollider == null)
        {
            zoneCollider = GetComponent<Collider>();
        }

        if (zoneCollider == null || !zoneCollider.enabled)
        {
            return false;
        }

        Vector3 closestPoint = zoneCollider.ClosestPoint(player.transform.position);
        return (closestPoint - player.transform.position).sqrMagnitude <= 0.0001f;
    }

    private static MinionTeam GetPlayerTeam(CatPlayerController player)
    {
        return PreferredPlayerFinder.IsPlayerTeam(player, MinionTeam.Cat) ? MinionTeam.Cat : MinionTeam.Dog;
    }

    private void ShowPrompt(CatPlayerController player)
    {
        if (player == null)
        {
            return;
        }

        if (promptObject == null)
        {
            promptObject = new GameObject("Build Site Prompt");
            promptText = promptObject.AddComponent<TextMesh>();
            promptText.anchor = TextAnchor.MiddleCenter;
            promptText.alignment = TextAlignment.Center;
            promptText.characterSize = 0.14f;
            promptText.fontSize = 32;
            promptText.color = Color.white;
        }

        if (currentBuilding != null)
        {
            HidePrompt();
            return;
        }

        promptObject.SetActive(true);
        promptText.text = "\u6309 " + openKey + " \u958b\u555f\u5efa\u7bc9\u9078\u55ae";
        promptObject.transform.position = player.transform.TransformPoint(promptLocalOffset);
        FaceCamera(promptObject.transform);
    }

    private void FlashPrompt(string message)
    {
        if (promptText != null)
        {
            promptText.text = message;
        }
    }

    private void HidePrompt()
    {
        if (promptObject != null)
        {
            promptObject.SetActive(false);
        }
    }

    private void ApplyOwnershipColor()
    {
        CacheDashedRangeRenderersIfNeeded();

        if (dashedRangeRenderers == null || dashedRangeRenderers.Length == 0)
        {
            return;
        }

        Color color = GetOwnerColor();
        if (dashedRangePropertyBlock == null)
        {
            dashedRangePropertyBlock = new MaterialPropertyBlock();
        }

        foreach (Renderer renderer in dashedRangeRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            LineRenderer lineRenderer = renderer as LineRenderer;
            if (lineRenderer != null)
            {
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
            }

            renderer.GetPropertyBlock(dashedRangePropertyBlock);
            dashedRangePropertyBlock.SetColor("_Color", color);
            dashedRangePropertyBlock.SetColor("_BaseColor", color);
            dashedRangePropertyBlock.SetColor("_EmissionColor", color);
            renderer.SetPropertyBlock(dashedRangePropertyBlock);
        }
    }

    private void CacheDashedRangeRenderersIfNeeded()
    {
        if (!autoFindDashedRangeRenderers || dashedRangeRenderers != null && dashedRangeRenderers.Length > 0)
        {
            return;
        }

        dashedRangeRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private Color GetOwnerColor()
    {
        if (!hasOwner)
        {
            return neutralColor;
        }

        return ownerTeam == MinionTeam.Cat ? catColor : dogColor;
    }

    private void CreateWhiteResourceBuildingModel(Transform parent, BuildSiteBuildingType type)
    {
        Material material = CreateWhiteMaterial();
        CreatePrimitive(parent, PrimitiveType.Cube, "White Model Body", new Vector3(0f, 0.7f, 0f), new Vector3(1.55f, 1.2f, 1.55f), material);
        CreatePrimitive(parent, PrimitiveType.Cylinder, "White Model Roof", new Vector3(0f, 1.45f, 0f), new Vector3(1.05f, 0.2f, 1.05f), material);
        PrimitiveType markerType = type == BuildSiteBuildingType.AutoQuarry ? PrimitiveType.Sphere : PrimitiveType.Cylinder;
        CreatePrimitive(parent, markerType, "White Model Resource Marker", new Vector3(0f, 1.85f, 0f), Vector3.one * 0.38f, material);
    }

    private static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string objectName, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = objectName;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localScale = localScale;

        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = primitive.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        return primitive;
    }

    private static Material CreateWhiteMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = "Build Site White Model";
        material.color = Color.white;
        material.SetColor("_BaseColor", Color.white);
        return material;
    }

    private static void FaceCamera(Transform target)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 direction = target.position - camera.transform.position;
        if (direction.sqrMagnitude > 0.001f)
        {
            target.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }

    private class BuildSiteUI : MonoBehaviour
    {
        private ArcherTowerBuildZone zone;
        private Canvas canvas;
        private Text resourceText;
        private Button archerButton;
        private Button lumberButton;
        private Button quarryButton;

        private void Awake()
        {
            BuildUI();
            Close();
        }

        public void Open(ArcherTowerBuildZone newZone, CatPlayerController builder)
        {
            zone = newZone;
            gameObject.SetActive(true);
            canvas.enabled = true;
            Refresh();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void Close()
        {
            if (canvas != null)
            {
                canvas.enabled = false;
            }

            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }

            Refresh();
        }

        private void BuildUI()
        {
            EnsureEventSystem();
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            gameObject.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject panel = CreatePanel(transform, new Vector2(680f, 430f));
            CreateText(panel.transform, font, "\u5efa\u7bc9\u9078\u55ae", 32, FontStyle.Bold, new Vector2(0f, 165f), new Vector2(520f, 44f));
            resourceText = CreateText(panel.transform, font, string.Empty, 18, FontStyle.Bold, new Vector2(0f, 125f), new Vector2(560f, 30f));

            archerButton = CreateBuildButton(panel.transform, font, BuildSiteBuildingType.ArcherTower, new Vector2(0f, 55f));
            lumberButton = CreateBuildButton(panel.transform, font, BuildSiteBuildingType.AutoLumber, new Vector2(0f, -45f));
            quarryButton = CreateBuildButton(panel.transform, font, BuildSiteBuildingType.AutoQuarry, new Vector2(0f, -145f));

            Button closeButton = CreateButton(panel.transform, font, "X", new Vector2(300f, 165f), new Vector2(44f, 44f));
            closeButton.onClick.AddListener(Close);
        }

        private Button CreateBuildButton(Transform parent, Font font, BuildSiteBuildingType type, Vector2 position)
        {
            string label = GetDisplayName(type) + "\n" + GetEffectText(type) + "\n" + GetCostText(type);
            Button button = CreateButton(parent, font, label, position, new Vector2(560f, 82f));
            button.onClick.AddListener(() =>
            {
                if (zone != null)
                {
                    zone.BeginBuild(type);
                }
            });
            return button;
        }

        private void Refresh()
        {
            if (zone == null)
            {
                return;
            }

            ResourceManager resources = ResourceManager.Instance;
            if (resourceText != null)
            {
                int coins = resources != null ? resources.Coins : 0;
                int wood = resources != null ? resources.Wood : 0;
                int stone = resources != null ? resources.Stone : 0;
                resourceText.text = "\u91d1\u5e63 " + coins + "    \u6728\u982d " + wood + "    \u77f3\u982d " + stone;
            }

            SetInteractable(archerButton, zone.CanAfford(BuildSiteBuildingType.ArcherTower));
            SetInteractable(lumberButton, zone.CanAfford(BuildSiteBuildingType.AutoLumber));
            SetInteractable(quarryButton, zone.CanAfford(BuildSiteBuildingType.AutoQuarry));
        }

        private static void SetInteractable(Button button, bool canUse)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = canUse;
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = canUse ? new Color(1f, 0.56f, 0.29f, 1f) : new Color(0.4f, 0.4f, 0.4f, 1f);
            }
        }

        private static GameObject CreatePanel(Transform parent, Vector2 size)
        {
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            SetRect(rect, Vector2.zero, size);
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.08f, 0.07f, 0.055f, 0.94f);
            return panel;
        }

        private static Button CreateButton(Transform parent, Font font, string label, Vector2 position, Vector2 size)
        {
            GameObject buttonObject = new GameObject("Button");
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            SetRect(rect, position, size);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(1f, 0.56f, 0.29f, 1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText(buttonObject.transform, font, label, 18, FontStyle.Bold, Vector2.zero, size);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static Text CreateText(Transform parent, Font font, string value, int size, FontStyle style, Vector2 position, Vector2 rectSize)
        {
            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = value;
            SetRect(text.rectTransform, position, rectSize);
            return text;
        }

        private static string GetCostText(BuildSiteBuildingType type)
        {
            string text = "\u9700\u8981 " + GetCoinCost(type) + "\u91d1\u5e63";
            if (GetWoodCost(type) > 0)
            {
                text += " / " + GetWoodCost(type) + "\u6728\u982d";
            }

            if (GetStoneCost(type) > 0)
            {
                text += " / " + GetStoneCost(type) + "\u77f3\u982d";
            }

            return text;
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

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
