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
        public bool usePrefabColliders = true;
    }

    [Header("Build Site")]
    [SerializeField] private KeyCode openKey = KeyCode.E;
    [SerializeField] private float buildDuration = 4f;
    [SerializeField] private int buildingHealth = 60;
    [SerializeField] private float builderCompletionClearance = 0.7f;
    [SerializeField] private bool useSimpleGameplayCollider = true;
    [SerializeField] private Transform buildAnchor;
    [SerializeField] private Vector3 buildLocalOffset = Vector3.zero;

    [Header("Building Visual Prefabs")]
    [SerializeField] private BuildingVisualPrefab[] buildingVisualPrefabs =
    {
        new BuildingVisualPrefab { type = BuildSiteBuildingType.ArcherTower, localScale = Vector3.one, usePrefabColliders = true },
        new BuildingVisualPrefab { type = BuildSiteBuildingType.AutoLumber, localScale = Vector3.one, usePrefabColliders = true },
        new BuildingVisualPrefab { type = BuildSiteBuildingType.AutoQuarry, localScale = Vector3.one, usePrefabColliders = true },
        new BuildingVisualPrefab { type = BuildSiteBuildingType.Barracks, localScale = Vector3.one, usePrefabColliders = true }
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
    [SerializeField] private float horizontalZoneEdgePadding = 0.05f;
    [SerializeField] private Vector3 promptLocalOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private string occupiedPromptText = "\u9700\u5148\u6467\u6bc0\u73fe\u6709\u5efa\u7bc9";

    [Header("Build Shop UI")]
    [SerializeField] private GameObject buildShopPrefab;

    private Collider zoneCollider;
    private CatPlayerController activeBuilder;
    private GameObject currentBuilding;
    private TeamBuilding currentTeamBuilding;
    private BuildSiteBuildingType currentBuildingType;
    private bool currentBuildingIsNetworkPrediction;
    private BuildSiteBuildingType pendingType;
    private bool isBuilding;
    private float buildProgress;
    private WorldSpaceHealthLabel promptLabel;
    private BuildShopUI ui;
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
        OnValidate();

        zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;
        CacheDashedRangeRenderersIfNeeded();
        ApplyOwnershipColor();
    }

    private void OnValidate()
    {
        buildDuration = Mathf.Max(0.1f, buildDuration);
        buildingHealth = Mathf.Max(1, buildingHealth);
        builderCompletionClearance = Mathf.Clamp(builderCompletionClearance, 0.1f, 1f);
        horizontalZoneEdgePadding = Mathf.Max(0f, horizontalZoneEdgePadding);
        barracksSummonInterval = Mathf.Max(1f, barracksSummonInterval);
        EnsureVisualPrefabSlots();
#if UNITY_EDITOR
        AutoAssignBuildShopPrefabIfMissing();
#endif
    }

    public bool HasCurrentBuilding => currentBuilding != null || isBuilding;
    public bool HasPlacedBuilding => currentBuilding != null;

    public Vector3 NetworkAnchorPosition
    {
        get
        {
            if (zoneCollider == null)
            {
                zoneCollider = GetComponent<Collider>();
            }

            return zoneCollider != null ? zoneCollider.bounds.center : transform.position;
        }
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
            normalized[i] = FindVisualPrefabSlot(existing, type) ?? new BuildingVisualPrefab { type = type, usePrefabColliders = true };
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

    private void AutoAssignBuildShopPrefabIfMissing()
    {
        if (buildShopPrefab != null)
        {
            return;
        }

        string[] guids = UnityEditor.AssetDatabase.FindAssets("buildCanvas t:Prefab", new[] { "Assets/prefab" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || !prefab.name.ToLowerInvariant().Contains("buildcanvas"))
            {
                continue;
            }

            buildShopPrefab = prefab;
            UnityEditor.EditorUtility.SetDirty(this);
            return;
        }
    }
#endif

    private void Update()
    {
        CatPlayerController builder = activeBuilder != null && IsPlayerInsideZone(activeBuilder)
            ? activeBuilder
            : FindPlayerInsideZone();
        if (builder == null)
        {
            HidePrompt();
            HideBuildUI();
            if (isBuilding)
            {
                CancelBuild();
            }

            activeBuilder = null;
            return;
        }

        activeBuilder = builder;

        if (isBuilding)
        {
            UpdateBuild(builder);
            return;
        }

        if (SettingsPanelUI.BlocksPlayerInput)
        {
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
        if (player == null || !IsPlayerInsideZone(player))
        {
            return;
        }

        activeBuilder = player;
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
        if (activeBuilder == null || HasCurrentBuilding)
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

    public bool TryBeginBuildFromUI(BuildSiteBuildingType type, out string failureMessage)
    {
        failureMessage = string.Empty;

        if (activeBuilder == null)
        {
            failureMessage = "\u9700\u8981\u7ad9\u5728\u5efa\u7bc9\u5340";
            FlashPrompt(failureMessage);
            return false;
        }

        if (HasCurrentBuilding)
        {
            failureMessage = occupiedPromptText;
            FlashPrompt(failureMessage);
            return false;
        }

        if (!CanAfford(type))
        {
            failureMessage = GetMissingCostText(type);
            FlashPrompt("\u8cc7\u6e90\u4e0d\u8db3\uff1a" + failureMessage);
            return false;
        }

        BeginBuild(type);
        return true;
    }

    private void UpdateBuild(CatPlayerController builder)
    {
        if (builder == null || !IsPlayerInsideZone(builder))
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
        MinionTeam team = GetPlayerTeam(builder);
        MoveBuilderOutsideBuildFootprint(builder, pendingType);
        if (TryRequestNetworkBuilding(pendingType, team, builder, out bool shouldCreatePrediction))
        {
            if (shouldCreatePrediction)
            {
                if (!SpendCost(pendingType))
                {
                    CancelBuild();
                    FlashPrompt("\u8cc7\u6e90\u4e0d\u8db3");
                    return;
                }

                TryCreatePredictedNetworkBuilding(pendingType, team);
            }
        }
        else
        {
            if (!SpendCost(pendingType))
            {
                CancelBuild();
                FlashPrompt("\u8cc7\u6e90\u4e0d\u8db3");
                return;
            }

            CreateBuilding(pendingType, team);
        }

        MoveBuilderOutsideCurrentBuilding(builder);
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
        if (HasCurrentBuilding)
        {
            FlashPrompt("\u9700\u5148\u6467\u6bc0\u73fe\u6709\u5efa\u7bc9");
            return;
        }

        EnsureBuildUI();
        if (ui != null)
        {
            ui.Open(this);
        }
    }

    private void HideBuildUI()
    {
        if (ui != null)
        {
            ui.CloseIfOpenedBy(this);
        }
    }

    private void EnsureBuildUI()
    {
        if (ui != null)
        {
            return;
        }

        ui = FindObjectOfType<BuildShopUI>(true);
        if (ui != null)
        {
            return;
        }

        if (buildShopPrefab == null)
        {
            Debug.LogWarning("Build zone needs the build shop prefab assigned.");
            return;
        }

        GameObject uiObject = Instantiate(buildShopPrefab);
        uiObject.name = buildShopPrefab.name + " Instance";
        if (buildShopPrefab != null)
        {
            UpgradeShopUI wrongShop = uiObject.GetComponent<UpgradeShopUI>();
            if (wrongShop != null)
            {
                Destroy(wrongShop);
            }
        }

        ui = uiObject.GetComponent<BuildShopUI>();
        if (ui == null)
        {
            ui = uiObject.AddComponent<BuildShopUI>();
        }
    }

    private void CreateBuilding(BuildSiteBuildingType type, MinionTeam team, bool isNetworkPrediction = false)
    {
        if (currentBuilding != null)
        {
            return;
        }

        GameObject buildingObject = new GameObject(team + " " + GetDisplayName(type));
        buildingObject.transform.position = GetBuildPosition();
        buildingObject.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        BuildingHealth health = buildingObject.AddComponent<BuildingHealth>();
        health.Configure(buildingHealth);
        health.Destroyed += OnBuildingDestroyed;

        currentBuilding = buildingObject;
        currentBuildingType = type;
        currentBuildingIsNetworkPrediction = isNetworkPrediction;
        currentTeamBuilding = buildingObject.AddComponent<TeamBuilding>();
        currentTeamBuilding.Configure(team);

        BoxCollider fallbackCollider = buildingObject.AddComponent<BoxCollider>();
        fallbackCollider.size = GetFootprint(type);
        fallbackCollider.center = new Vector3(0f, fallbackCollider.size.y * 0.5f, 0f);

        if (type == BuildSiteBuildingType.ArcherTower)
        {
            ArcherTower tower = buildingObject.AddComponent<ArcherTower>();
            tower.Configure(team, towerAttackRange, towerShotsPerSecond, towerDamage, towerProjectileSpeed);
            CreateBuildingVisual(buildingObject.transform, type, fallbackCollider);
        }
        else if (type == BuildSiteBuildingType.Barracks)
        {
            BarracksBuilding barracks = buildingObject.AddComponent<BarracksBuilding>();
            barracks.Configure(team, barracksSummonInterval);
            CreateBuildingVisual(buildingObject.transform, type, fallbackCollider);
        }
        else
        {
            AutoResourceBuilding producer = buildingObject.AddComponent<AutoResourceBuilding>();
            producer.Configure(type == BuildSiteBuildingType.AutoQuarry ? ResourceType.Stone : ResourceType.Wood, 1, 2f);
            CreateBuildingVisual(buildingObject.transform, type, fallbackCollider);
        }

        SetOwner(team);
    }

    public bool TryCreateNetworkBuilding(BuildSiteBuildingType type, MinionTeam team)
    {
        if (currentBuilding != null)
        {
            if (currentBuildingIsNetworkPrediction)
            {
                if (currentBuildingType != type || currentTeamBuilding == null || currentTeamBuilding.Team != team)
                {
                    ClearCurrentBuilding();
                    CreateBuilding(type, team);
                    return currentBuilding != null;
                }

                currentBuildingIsNetworkPrediction = false;
                SetOwner(team);
                return true;
            }

            return false;
        }

        CreateBuilding(type, team);
        return currentBuilding != null;
    }

    public bool TryCreatePredictedNetworkBuilding(BuildSiteBuildingType type, MinionTeam team)
    {
        if (currentBuilding != null)
        {
            return false;
        }

        CreateBuilding(type, team, true);
        return currentBuilding != null;
    }

    public bool RejectNetworkBuildPrediction(BuildSiteBuildingType type)
    {
        if (!currentBuildingIsNetworkPrediction || currentBuildingType != type)
        {
            return false;
        }

        ClearCurrentBuilding();
        return true;
    }

    public static ArcherTowerBuildZone FindClosestNetworkZone(Vector3 anchorPosition, float maxDistance = 2.5f)
    {
        ArcherTowerBuildZone[] zones = FindObjectsOfType<ArcherTowerBuildZone>(true);
        ArcherTowerBuildZone closest = null;
        float closestDistance = maxDistance * maxDistance;

        for (int i = 0; i < zones.Length; i++)
        {
            ArcherTowerBuildZone zone = zones[i];
            if (zone == null)
            {
                continue;
            }

            Vector3 offset = zone.NetworkAnchorPosition - anchorPosition;
            offset.y = 0f;
            float distance = offset.sqrMagnitude;
            if (distance <= closestDistance)
            {
                closest = zone;
                closestDistance = distance;
            }
        }

        return closest;
    }

    public static void RefundBuildCost(BuildSiteBuildingType type)
    {
        ResourceManager resources = ResourceManager.Instance;
        if (resources == null)
        {
            return;
        }

        int coinCost = GetCoinCost(type);
        int woodCost = GetWoodCost(type);
        int stoneCost = GetStoneCost(type);

        if (coinCost > 0)
        {
            resources.Add(ResourceType.Coin, coinCost);
        }

        if (woodCost > 0)
        {
            resources.Add(ResourceType.Wood, woodCost);
        }

        if (stoneCost > 0)
        {
            resources.Add(ResourceType.Stone, stoneCost);
        }
    }

    public static bool TrySpendBuildCost(BuildSiteBuildingType type)
    {
        ResourceManager resources = ResourceManager.Instance;
        if (resources == null)
        {
            return false;
        }

        int coinCost = GetCoinCost(type);
        int woodCost = GetWoodCost(type);
        int stoneCost = GetStoneCost(type);
        if (!resources.CanSpend(ResourceType.Coin, coinCost)
            || !resources.CanSpend(ResourceType.Wood, woodCost)
            || !resources.CanSpend(ResourceType.Stone, stoneCost))
        {
            return false;
        }

        if (coinCost > 0)
        {
            resources.Spend(ResourceType.Coin, coinCost);
        }

        if (woodCost > 0)
        {
            resources.Spend(ResourceType.Wood, woodCost);
        }

        if (stoneCost > 0)
        {
            resources.Spend(ResourceType.Stone, stoneCost);
        }

        return true;
    }

    private bool TryRequestNetworkBuilding(BuildSiteBuildingType type, MinionTeam team, CatPlayerController builder, out bool shouldCreatePrediction)
    {
        shouldCreatePrediction = false;
        UglyDogNetworkPlayer networkPlayer = builder != null ? builder.GetComponent<UglyDogNetworkPlayer>() : null;
        if (networkPlayer == null || !builder.HasRunningNetworkInputAuthority())
        {
            return false;
        }

        shouldCreatePrediction = networkPlayer.ShouldPredictBuildRequests;
        return networkPlayer.RequestBuild(NetworkAnchorPosition, type, team);
    }

    private void OnBuildingDestroyed(BuildingHealth health)
    {
        if (health != null)
        {
            health.Destroyed -= OnBuildingDestroyed;
        }

        ResetCurrentBuildingState();
    }

    private void ClearCurrentBuilding()
    {
        if (currentTeamBuilding != null && currentTeamBuilding.Health != null)
        {
            currentTeamBuilding.Health.Destroyed -= OnBuildingDestroyed;
        }

        if (currentBuilding != null)
        {
            Destroy(currentBuilding);
        }

        ResetCurrentBuildingState();
    }

    private void ResetCurrentBuildingState()
    {
        currentBuilding = null;
        currentTeamBuilding = null;
        currentBuildingType = default;
        currentBuildingIsNetworkPrediction = false;
        hasOwner = false;
        ApplyOwnershipColor();
    }

    private Vector3 GetBuildPosition()
    {
        return buildAnchor != null ? buildAnchor.position : transform.TransformPoint(buildLocalOffset);
    }

    private void MoveBuilderOutsideBuildFootprint(CatPlayerController builder, BuildSiteBuildingType type)
    {
        if (builder == null)
        {
            return;
        }

        Vector3 footprint = GetFootprint(type);
        Vector3 buildPosition = GetBuildPosition();
        Quaternion buildRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Vector3 localOffset = Quaternion.Inverse(buildRotation) * (builder.transform.position - buildPosition);

        float halfX = footprint.x * 0.5f + builderCompletionClearance;
        float halfZ = footprint.z * 0.5f + builderCompletionClearance;
        bool insideX = Mathf.Abs(localOffset.x) < halfX;
        bool insideZ = Mathf.Abs(localOffset.z) < halfZ;
        if (!insideX || !insideZ)
        {
            return;
        }

        float pushX = halfX - Mathf.Abs(localOffset.x);
        float pushZ = halfZ - Mathf.Abs(localOffset.z);
        Vector3 localDirection;
        float distance;
        if (pushX < pushZ)
        {
            localDirection = new Vector3(localOffset.x >= 0f ? 1f : -1f, 0f, 0f);
            distance = pushX;
        }
        else
        {
            localDirection = new Vector3(0f, 0f, localOffset.z >= 0f ? 1f : -1f);
            distance = pushZ;
        }

        localOffset += localDirection * (distance + builderCompletionClearance);
        Vector3 resolvedPosition = buildPosition + buildRotation * localOffset;
        resolvedPosition.y = builder.transform.position.y;
        builder.TeleportToGroundedPosition(resolvedPosition);
    }

    private void MoveBuilderOutsideCurrentBuilding(CatPlayerController builder)
    {
        if (builder == null || currentBuilding == null || !TryGetBuildingColliderBounds(currentBuilding, out Bounds bounds))
        {
            return;
        }

        Vector3 position = builder.transform.position;
        float safetyMargin = builderCompletionClearance + 0.6f;
        bool insideX = position.x > bounds.min.x - safetyMargin && position.x < bounds.max.x + safetyMargin;
        bool insideZ = position.z > bounds.min.z - safetyMargin && position.z < bounds.max.z + safetyMargin;
        if (!insideX || !insideZ)
        {
            return;
        }

        Vector3 exitDirection = GetBuilderExitDirection(builder, bounds);
        float safeHalfX = bounds.extents.x + safetyMargin;
        float safeHalfZ = bounds.extents.z + safetyMargin;
        float distanceToXEdge = Mathf.Abs(exitDirection.x) > 0.001f
            ? safeHalfX / Mathf.Abs(exitDirection.x)
            : float.PositiveInfinity;
        float distanceToZEdge = Mathf.Abs(exitDirection.z) > 0.001f
            ? safeHalfZ / Mathf.Abs(exitDirection.z)
            : float.PositiveInfinity;
        float exitDistance = Mathf.Min(distanceToXEdge, distanceToZEdge) + builderCompletionClearance;
        Vector3 resolvedPosition = bounds.center + exitDirection * exitDistance;
        resolvedPosition.y = position.y;

        builder.TeleportToGroundedPosition(resolvedPosition);
    }

    private void MoveBuilderOutsideBuildZone(CatPlayerController builder)
    {
        if (builder == null || zoneCollider == null)
        {
            return;
        }

        Bounds bounds = zoneCollider.bounds;
        Vector3 position = builder.transform.position;
        bool insideX = position.x > bounds.min.x - builderCompletionClearance && position.x < bounds.max.x + builderCompletionClearance;
        bool insideZ = position.z > bounds.min.z - builderCompletionClearance && position.z < bounds.max.z + builderCompletionClearance;
        if (!insideX || !insideZ)
        {
            return;
        }

        Vector3 exitDirection = position - GetBuildPosition();
        exitDirection.y = 0f;
        if (exitDirection.sqrMagnitude < 0.001f)
        {
            exitDirection = position - bounds.center;
            exitDirection.y = 0f;
        }

        if (exitDirection.sqrMagnitude < 0.001f)
        {
            exitDirection = -transform.forward;
            exitDirection.y = 0f;
        }

        exitDirection = exitDirection.sqrMagnitude > 0.001f ? exitDirection.normalized : Vector3.back;
        float safetyMargin = builderCompletionClearance + 0.8f;
        float safeHalfX = bounds.extents.x + safetyMargin;
        float safeHalfZ = bounds.extents.z + safetyMargin;
        float distanceToXEdge = Mathf.Abs(exitDirection.x) > 0.001f
            ? safeHalfX / Mathf.Abs(exitDirection.x)
            : float.PositiveInfinity;
        float distanceToZEdge = Mathf.Abs(exitDirection.z) > 0.001f
            ? safeHalfZ / Mathf.Abs(exitDirection.z)
            : float.PositiveInfinity;
        float exitDistance = Mathf.Min(distanceToXEdge, distanceToZEdge);
        Vector3 resolvedPosition = bounds.center + exitDirection * exitDistance;
        resolvedPosition.y = position.y;

        builder.TeleportToGroundedPosition(resolvedPosition);
    }

    private Vector3 GetBuilderExitDirection(CatPlayerController builder, Bounds bounds)
    {
        Vector3 direction = builder.transform.position - bounds.center;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = builder.transform.position - GetBuildPosition();
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.001f)
        {
            direction = -transform.forward;
            direction.y = 0f;
        }

        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.back;
    }

    private static bool TryGetBuildingColliderBounds(GameObject building, out Bounds bounds)
    {
        bounds = default;
        Collider[] colliders = building.GetComponentsInChildren<Collider>(true);
        bool found = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            if (!found)
            {
                bounds = collider.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return found;
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

    public string GetMissingCostText(BuildSiteBuildingType type)
    {
        ResourceManager resources = ResourceManager.Instance;
        int coins = resources != null ? resources.Coins : 0;
        int wood = resources != null ? resources.Wood : 0;
        int stone = resources != null ? resources.Stone : 0;

        string text = string.Empty;
        AppendMissingCost(ref text, "\u91d1\u5e63", GetCoinCost(type), coins);
        AppendMissingCost(ref text, "\u6728\u982d", GetWoodCost(type), wood);
        AppendMissingCost(ref text, "\u77f3\u982d", GetStoneCost(type), stone);
        return string.IsNullOrEmpty(text) ? GetCostText(type) : text;
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
                return "\u6bcf 2 \u79d2 +1 \u6728\u982d";
            case BuildSiteBuildingType.AutoQuarry:
                return "\u6bcf 2 \u79d2 +1 \u77f3\u982d";
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

    public static string GetCostText(BuildSiteBuildingType type)
    {
        string text = GetCoinCost(type) + "\u91d1\u5e63";
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

    private static void AppendMissingCost(ref string text, string label, int required, int current)
    {
        int missing = Mathf.Max(0, required - current);
        if (missing <= 0)
        {
            return;
        }

        if (!string.IsNullOrEmpty(text))
        {
            text += " / ";
        }

        text += missing + label;
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
            if (player != null && IsPlayerInsideZone(player))
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

        return IsPointInsideHorizontalZone(player.transform.position);
    }

    private bool IsPointInsideHorizontalZone(Vector3 position)
    {
        if (zoneCollider == null)
        {
            return false;
        }

        Bounds bounds = zoneCollider.bounds;
        Vector3 center = bounds.center;
        float radius = Mathf.Max(0.05f, Mathf.Min(bounds.extents.x, bounds.extents.z) - horizontalZoneEdgePadding);
        Vector2 offset = new Vector2(position.x - center.x, position.z - center.z);
        return offset.sqrMagnitude <= radius * radius;
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

    private bool CreateBuildingVisual(Transform parent, BuildSiteBuildingType type, Collider fallbackCollider)
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
        Collider[] visualColliders = visualObject.GetComponentsInChildren<Collider>(true);
        if (!useSimpleGameplayCollider && visual.usePrefabColliders && visualColliders.Length == 0)
        {
            AddMeshCollidersFromVisualMeshes(visualObject);
            visualColliders = visualObject.GetComponentsInChildren<Collider>(true);
        }

        if (!useSimpleGameplayCollider && visual.usePrefabColliders && visualColliders.Length > 0)
        {
            if (fallbackCollider != null)
            {
                fallbackCollider.enabled = false;
            }

            SetVisualCollidersEnabled(visualColliders, true);
        }
        else
        {
            if (fallbackCollider != null)
            {
                fallbackCollider.enabled = true;
                FitColliderToPrefabColliderBounds(fallbackCollider as BoxCollider, visualColliders);
            }

            RemoveVisualColliders(visualColliders);
        }

        return true;
    }

    private static void FitColliderToPrefabColliderBounds(BoxCollider collider, Collider[] visualColliders)
    {
        if (collider == null || visualColliders == null || visualColliders.Length == 0)
        {
            return;
        }

        if (!TryGetColliderLocalBounds(collider.transform, visualColliders, out Bounds localBounds))
        {
            return;
        }

        collider.center = localBounds.center;
        collider.size = new Vector3(
            Mathf.Max(0.1f, localBounds.size.x),
            Mathf.Max(0.5f, localBounds.size.y),
            Mathf.Max(0.1f, localBounds.size.z));
    }

    private static bool TryGetColliderLocalBounds(Transform root, Collider[] colliders, out Bounds localBounds)
    {
        localBounds = default;
        bool found = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            EncapsulateWorldBoundsAsLocal(root, collider.bounds, ref localBounds, ref found);
        }

        return found;
    }

    private static void EncapsulateWorldBoundsAsLocal(Transform root, Bounds worldBounds, ref Bounds localBounds, ref bool found)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
        {
            Vector3 localPoint = root.InverseTransformPoint(corners[cornerIndex]);
            if (!found)
            {
                localBounds = new Bounds(localPoint, Vector3.zero);
                found = true;
            }
            else
            {
                localBounds.Encapsulate(localPoint);
            }
        }
    }

    private static void AddMeshCollidersFromVisualMeshes(GameObject visualObject)
    {
        MeshFilter[] meshFilters = visualObject.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            MeshCollider collider = meshFilter.GetComponent<MeshCollider>();
            if (collider == null)
            {
                collider = meshFilter.gameObject.AddComponent<MeshCollider>();
            }

            collider.sharedMesh = meshFilter.sharedMesh;
            collider.convex = false;
        }
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

    private static void SetVisualCollidersEnabled(Collider[] colliders, bool enabled)
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = enabled;
        }
    }

    private static void RemoveVisualColliders(Collider[] colliders)
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
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
