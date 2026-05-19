using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum BuildSiteBuildingType
{
    ArcherTower,
    AutoLumber,
    AutoQuarry,
    Barracks
}

[RequireComponent(typeof(Collider))]
public class ArcherTowerBuildZone : MonoBehaviour
{
    [System.Serializable]
    private class BuildingVisualPrefab
    {
        public BuildSiteBuildingType type;
        public GameObject prefab;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
    }

    [Header("Build Site")]
    [SerializeField] private KeyCode openKey = KeyCode.E;
    [SerializeField] private float buildDuration = 4f;
    [SerializeField] private int buildingHealth = 60;
    [SerializeField] private Transform buildAnchor;
    [SerializeField] private Vector3 buildLocalOffset = Vector3.zero;

    [Header("Building Visual Prefabs")]
    [SerializeField] private BuildingVisualPrefab[] buildingVisualPrefabs =
    {
        new BuildingVisualPrefab { type = BuildSiteBuildingType.ArcherTower, localScale = Vector3.one },
        new BuildingVisualPrefab { type = BuildSiteBuildingType.AutoLumber, localScale = Vector3.one },
        new BuildingVisualPrefab { type = BuildSiteBuildingType.AutoQuarry, localScale = Vector3.one },
        new BuildingVisualPrefab { type = BuildSiteBuildingType.Barracks, localScale = Vector3.one }
    };

    [Header("Archer Tower")]
    [SerializeField] private float towerAttackRange = 8f;
    [SerializeField] private float towerShotsPerSecond = 1f;
    [SerializeField] private int towerDamage = 5;
    [SerializeField] private float towerProjectileSpeed = 12f;

    [Header("Barracks")]
    [SerializeField] private float barracksSummonInterval = 12f;

    [Header("Prepared Dashed Range")]
    [SerializeField] private bool autoFindDashedRangeRenderers = true;
    [SerializeField] private Renderer[] dashedRangeRenderers;
    [SerializeField] private Color neutralColor = Color.white;
    [SerializeField] private Color dogColor = new Color(1f, 0.682f, 0f, 1f);
    [SerializeField] private Color catColor = new Color(0f, 0.847f, 1f, 1f);

    [Header("Detection")]
    [SerializeField] private LayerMask detectionLayers = ~0;
    [SerializeField] private Vector3 promptLocalOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private string occupiedPromptText = "\u9700\u5148\u6467\u6bc0\u73fe\u6709\u5efa\u7bc9";

    private Collider zoneCollider;
    private CatPlayerController activeBuilder;
    private GameObject currentBuilding;
    private TeamBuilding currentTeamBuilding;
    private BuildSiteBuildingType pendingType;
    private bool isBuilding;
    private float buildProgress;
    private WorldSpaceHealthLabel promptLabel;
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
        barracksSummonInterval = Mathf.Max(1f, barracksSummonInterval);
        EnsureVisualPrefabSlots();
    }

    private void EnsureVisualPrefabSlots()
    {
        System.Array values = System.Enum.GetValues(typeof(BuildSiteBuildingType));
        int requiredCount = values.Length;
        if (buildingVisualPrefabs == null)
        {
            buildingVisualPrefabs = new BuildingVisualPrefab[0];
        }

        if (buildingVisualPrefabs.Length >= requiredCount && HasAllVisualPrefabSlots(values))
        {
            NormalizeVisualPrefabScales();
            return;
        }

        BuildingVisualPrefab[] existing = buildingVisualPrefabs;
        BuildingVisualPrefab[] normalized = new BuildingVisualPrefab[requiredCount];
        for (int i = 0; i < requiredCount; i++)
        {
            BuildSiteBuildingType type = (BuildSiteBuildingType)values.GetValue(i);
            normalized[i] = FindVisualPrefabSlot(existing, type) ?? new BuildingVisualPrefab { type = type };
            if (normalized[i].localScale == Vector3.zero)
            {
                normalized[i].localScale = Vector3.one;
            }
        }

        buildingVisualPrefabs = normalized;
    }

    private bool HasAllVisualPrefabSlots(System.Array values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (FindVisualPrefabSlot(buildingVisualPrefabs, (BuildSiteBuildingType)values.GetValue(i)) == null)
            {
                return false;
            }
        }

        return true;
    }

    private void NormalizeVisualPrefabScales()
    {
        for (int i = 0; i < buildingVisualPrefabs.Length; i++)
        {
            if (buildingVisualPrefabs[i] != null && buildingVisualPrefabs[i].localScale == Vector3.zero)
            {
                buildingVisualPrefabs[i].localScale = Vector3.one;
            }
        }
    }

    private static BuildingVisualPrefab FindVisualPrefabSlot(BuildingVisualPrefab[] slots, BuildSiteBuildingType type)
    {
        if (slots == null)
        {
            return null;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].type == type)
            {
                return slots[i];
            }
        }

        return null;
    }

#if UNITY_EDITOR
    [ContextMenu("Auto Assign Building Visual Prefabs")]
    private void AutoAssignBuildingVisualPrefabs()
    {
        EnsureVisualPrefabSlots();
        for (int i = 0; i < buildingVisualPrefabs.Length; i++)
        {
            BuildingVisualPrefab slot = buildingVisualPrefabs[i];
            if (slot == null || slot.prefab != null)
            {
                continue;
            }

            slot.prefab = FindPrefabForBuildingType(slot.type);
        }

        UnityEditor.EditorUtility.SetDirty(this);
    }

    private static GameObject FindPrefabForBuildingType(BuildSiteBuildingType type)
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        string[] exactNames = GetExactPrefabNames(type);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (ContainsExact(fileName, exactNames))
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (!IsPrefabNameMatch(type, fileName))
            {
                continue;
            }

            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        return null;
    }

    private static bool IsPrefabNameMatch(BuildSiteBuildingType type, string fileName)
    {
        switch (type)
        {
            case BuildSiteBuildingType.ArcherTower:
                return ContainsAny(fileName, "tower", "archertower", "archer_tower", "archer tower", "\u5f13\u7bad\u5854");
            case BuildSiteBuildingType.AutoLumber:
                return ContainsAny(fileName, "woodmachine", "wood_machine", "autolumber", "auto_lumber", "lumber", "woodcutter", "sawmill", "\u4f10\u6728");
            case BuildSiteBuildingType.AutoQuarry:
                return ContainsAny(fileName, "stonemachine", "stone_machine", "autoquarry", "auto_quarry", "quarry", "\u63a1\u77f3", "\u91c7\u77f3");
            case BuildSiteBuildingType.Barracks:
                return ContainsAny(fileName, "camp", "barracks", "barrack", "\u5175\u71df");
            default:
                return false;
        }
    }

    private static string[] GetExactPrefabNames(BuildSiteBuildingType type)
    {
        switch (type)
        {
            case BuildSiteBuildingType.ArcherTower:
                return new[] { "tower" };
            case BuildSiteBuildingType.AutoLumber:
                return new[] { "woodmachine" };
            case BuildSiteBuildingType.AutoQuarry:
                return new[] { "stonemachine" };
            case BuildSiteBuildingType.Barracks:
                return new[] { "camp" };
            default:
                return new string[0];
        }
    }

    private static bool ContainsExact(string value, string[] exactValues)
    {
        for (int i = 0; i < exactValues.Length; i++)
        {
            if (value == exactValues[i])
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAny(string value, params string[] keywords)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (value.Contains(keywords[i]))
            {
                return true;
            }
        }

        return false;
    }
#endif

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
        FlashPrompt(GetBuildCountdownText());
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
        FlashPrompt(GetBuildCountdownText());

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
        buildingObject.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

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
            CreateBuildingVisual(buildingObject.transform, type);
        }
        else if (type == BuildSiteBuildingType.Barracks)
        {
            BarracksBuilding barracks = buildingObject.AddComponent<BarracksBuilding>();
            barracks.Configure(team, barracksSummonInterval);
            CreateBuildingVisual(buildingObject.transform, type);
        }
        else
        {
            AutoResourceBuilding producer = buildingObject.AddComponent<AutoResourceBuilding>();
            producer.Configure(type == BuildSiteBuildingType.AutoQuarry ? ResourceType.Stone : ResourceType.Wood, 1, 5f);
            CreateBuildingVisual(buildingObject.transform, type);
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
            case BuildSiteBuildingType.Barracks:
                return "\u5175\u71df";
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
            case BuildSiteBuildingType.Barracks:
                return "\u6bcf 12 \u79d2\u53ec\u559a 1 \u96bb\u8fd1\u6230\u5c0f\u5175";
            default:
                return string.Empty;
        }
    }

    public static int GetCoinCost(BuildSiteBuildingType type)
    {
        if (type == BuildSiteBuildingType.Barracks)
        {
            return 150;
        }

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
        if (type == BuildSiteBuildingType.ArcherTower)
        {
            return new Vector3(1.6f, 2.6f, 1.6f);
        }

        if (type == BuildSiteBuildingType.Barracks)
        {
            return new Vector3(2.6f, 1.9f, 2.2f);
        }

        return new Vector3(2.2f, 1.8f, 2.2f);
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

        if (promptLabel == null)
        {
            promptLabel = WorldSpaceHealthLabel.Create(
                player.transform,
                GetPromptLabelName(),
                promptLocalOffset,
                30,
                new Vector2(280f, 56f),
                0.01f);
        }
        else if (promptLabel.transform.parent != player.transform)
        {
            promptLabel.AttachTo(player.transform, promptLocalOffset);
        }

        promptLabel.gameObject.SetActive(true);
        promptLabel.SetText(currentBuilding != null ? occupiedPromptText : GetOpenPromptText());
    }

    private string GetPromptLabelName()
    {
        return "Build Site Prompt " + GetInstanceID();
    }

    private void FlashPrompt(string message)
    {
        if (promptLabel != null)
        {
            promptLabel.SetText(message);
        }
    }

    private string GetOpenPromptText()
    {
        return "\u6309" + openKey + "\u6253\u958b\u5efa\u7bc9\u5217\u8868";
    }

    private string GetBuildCountdownText()
    {
        int remainingSeconds = Mathf.CeilToInt(Mathf.Max(0f, buildDuration - buildProgress));
        return "\u5269\u9918 " + remainingSeconds + " \u79d2\u5b8c\u6210\u5efa\u7bc9";
    }

    private void HidePrompt()
    {
        if (promptLabel != null)
        {
            promptLabel.gameObject.SetActive(false);
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

    private bool CreateBuildingVisual(Transform parent, BuildSiteBuildingType type)
    {
        BuildingVisualPrefab visual = GetBuildingVisualPrefab(type);
        if (visual == null || visual.prefab == null)
        {
            Debug.LogWarning("Build zone has no visual prefab assigned for " + type + ".");
            return false;
        }

        GameObject visualObject = Instantiate(visual.prefab, parent, false);
        Quaternion prefabLocalRotation = visualObject.transform.localRotation;
        Vector3 prefabLocalScale = visualObject.transform.localScale;
        Vector3 scaleMultiplier = visual.localScale == Vector3.zero ? Vector3.one : visual.localScale;

        visualObject.name = visual.prefab.name + " Visual";
        visualObject.transform.localPosition = visual.localPosition;
        visualObject.transform.localRotation = prefabLocalRotation * Quaternion.Euler(visual.localEulerAngles);
        visualObject.transform.localScale = Vector3.Scale(prefabLocalScale, scaleMultiplier);
        DisableVisualColliders(visualObject);
        return true;
    }

    private BuildingVisualPrefab GetBuildingVisualPrefab(BuildSiteBuildingType type)
    {
        if (buildingVisualPrefabs == null)
        {
            return null;
        }

        for (int i = 0; i < buildingVisualPrefabs.Length; i++)
        {
            BuildingVisualPrefab visual = buildingVisualPrefabs[i];
            if (visual != null && visual.type == type)
            {
#if UNITY_EDITOR
                if (visual.prefab == null)
                {
                    visual.prefab = FindPrefabForBuildingType(type);
                    if (visual.prefab != null)
                    {
                        UnityEditor.EditorUtility.SetDirty(this);
                    }
                }
#endif
                return visual;
            }
        }

        return null;
    }

    private static void DisableVisualColliders(GameObject visualObject)
    {
        Collider[] colliders = visualObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
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
        private Button barracksButton;

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
            GameObject panel = CreatePanel(transform, new Vector2(680f, 520f));
            CreateText(panel.transform, font, "\u5efa\u7bc9\u9078\u55ae", 32, FontStyle.Bold, new Vector2(0f, 210f), new Vector2(520f, 44f));
            resourceText = CreateText(panel.transform, font, string.Empty, 18, FontStyle.Bold, new Vector2(0f, 170f), new Vector2(560f, 30f));

            archerButton = CreateBuildButton(panel.transform, font, BuildSiteBuildingType.ArcherTower, new Vector2(0f, 100f));
            barracksButton = CreateBuildButton(panel.transform, font, BuildSiteBuildingType.Barracks, new Vector2(0f, 0f));
            lumberButton = CreateBuildButton(panel.transform, font, BuildSiteBuildingType.AutoLumber, new Vector2(0f, -100f));
            quarryButton = CreateBuildButton(panel.transform, font, BuildSiteBuildingType.AutoQuarry, new Vector2(0f, -200f));

            Button closeButton = CreateButton(panel.transform, font, "X", new Vector2(300f, 210f), new Vector2(44f, 44f));
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
            SetInteractable(barracksButton, zone.CanAfford(BuildSiteBuildingType.Barracks));
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
