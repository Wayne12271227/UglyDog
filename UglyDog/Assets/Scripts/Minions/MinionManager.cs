using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MinionManager : MonoBehaviour
{
    public static MinionManager Instance { get; private set; }
    public const float PlayerManualSummonCooldownSeconds = 1.5f;

    [Header("Lane Points")]
    [SerializeField] private Transform dogSpawnPoint;
    [SerializeField] private Transform dogGoalPoint;
    [SerializeField] private Transform catSpawnPoint;
    [SerializeField] private Transform catGoalPoint;

    [Header("Costs")]
    [SerializeField] private int meleeCost = 12;
    [SerializeField] private int rangedCost = 18;

    [Header("Melee")]
    [SerializeField] private int meleeHealth = 32;
    [SerializeField] private int meleeDamage = 5;
    [SerializeField] private float meleeAttackRange = 1.15f;
    [SerializeField] private float meleeAttackCooldown = 0.9f;
    [SerializeField] private float meleeMoveSpeed = 3f;

    [Header("Ranged")]
    [SerializeField] private int rangedHealth = 20;
    [SerializeField] private int rangedDamage = 4;
    [SerializeField] private float rangedAttackRange = 8f;
    [SerializeField] private float rangedAttackCooldown = 1.25f;
    [SerializeField] private float rangedMoveSpeed = 3f;

    [Header("Search")]
    [SerializeField] private float targetSearchRadius = 7f;
    [SerializeField] private float buildingSearchRadius = 24f;
    [SerializeField] private float buildingPriorityRadius = 18f;
    [SerializeField] private float minionInterruptRadius = 2.2f;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.1f, 1.6f);
    [SerializeField] private int baseHealth = 100;
    [SerializeField] private Vector3 baseHealthLabelOffset = new Vector3(2.6f, 1.7f, 0f);
    [SerializeField] private bool createSinglePlayerCatStandIn = true;

    [Header("Cat Minion Prefabs")]
    [SerializeField] private GameObject catMeleeVisualPrefab;
    [SerializeField] private GameObject catRangedVisualPrefab;
    [SerializeField] private float catMeleeVisualYawOffset = 180f;
    [SerializeField] private float catRangedVisualYawOffset = 180f;

    [Header("Dog Minion Prefabs")]
    [SerializeField] private GameObject dogMeleeVisualPrefab;
    [SerializeField] private GameObject dogRangedVisualPrefab;
    [SerializeField] private float dogMeleeVisualYawOffset = 0f;
    [SerializeField] private float dogRangedVisualYawOffset = 0f;

    [Header("Victory Result")]
    [SerializeField] private GameObject dogVictoryPrefab;
    [SerializeField] private GameObject catVictoryPrefab;
    [SerializeField] private VictoryResultView victoryResultPrefab;

    private static Material whiteModelMaterial;
    private static Material dogTeamMaterial;
    private static Material catTeamMaterial;
    private static Material minionOutlineMaterial;
    private const float VisualGroundSink = 0.055f;
    private const float MinionOutlineWidth = 0.0065f;
    private MinionBaseHealth dogBase;
    private MinionBaseHealth catBase;
    private GameObject singlePlayerCatStandIn;
    private GameObject singlePlayerCatPrefab;
    private bool humanCatOpponentPresent;
    private bool gameEnded;
    private VictoryResultView resultView;
    private Canvas resultCanvas;
    private Text resultText;
    private RawImage resultCharacterImage;
    private RenderTexture resultCharacterTexture;
    private GameObject resultCharacterStage;
    private GameObject resultCharacterInstance;
    private Button resultConfirmButton;
    private Text resultConfirmButtonText;
    private MinionTeam pendingWinningTeam;
    private float matchStartUnscaledTime;

    private const string CatMeleeVisualPath = "Assets/prefab/cat_melee.prefab";
    private const string CatRangedVisualPath = "Assets/prefab/cat_ranged.prefab";
    private const string DogMeleeVisualPath = "Assets/prefab/dog_melee.prefab";
    private const string DogRangedVisualPath = "Assets/prefab/dog_ranged.prefab";
    private const string DogVictoryPrefabPath = "Assets/prefab/character/DOG.prefab";
    private const string CatVictoryPrefabPath = "Assets/prefab/character/CAT2 1.prefab";
    private const string VictoryResultPrefabPath = "Assets/prefab/VictoryResultCanvas.prefab";
    private const string MainMenuSceneName = "MainMenu";
    private const string SettlementSceneName = "settlement";
    private const int VictoryPreviewLayer = 31;
    private const int VictoryPreviewMask = 1 << VictoryPreviewLayer;
    private static float nextPlayerManualSummonTime;

    public static MinionManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        MinionManager existing = FindObjectOfType<MinionManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("Minion Manager");
        return managerObject.AddComponent<MinionManager>();
    }

    public static Material GetTeamMaterial(MinionTeam team)
    {
        EnsureSharedMaterials();
        return team == MinionTeam.Dog ? dogTeamMaterial : catTeamMaterial;
    }

    public static bool IsPlayerManualSummonReady()
    {
        return Time.time >= nextPlayerManualSummonTime;
    }

    public static float GetPlayerManualSummonCooldownRemaining()
    {
        return Mathf.Max(0f, nextPlayerManualSummonTime - Time.time);
    }

    public static void StartPlayerManualSummonCooldown()
    {
        nextPlayerManualSummonTime = Time.time + PlayerManualSummonCooldownSeconds;
    }

    public static string GetPlayerManualSummonCooldownText()
    {
        return "\u53ec\u559a\u51b7\u537b\u4e2d " + GetPlayerManualSummonCooldownRemaining().ToString("0.0") + "\u79d2";
    }

    public bool IsGameEnded => gameEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
        gameEnded = false;
        matchStartUnscaledTime = Time.unscaledTime;
        nextPlayerManualSummonTime = 0f;
        EnsureSharedMaterials();
        ResolveLanePoints();
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        Time.timeScale = 1f;
        DestroyResultCharacterStage();
        Instance = null;
    }

    private void OnValidate()
    {
        baseHealth = Mathf.Max(1, baseHealth);
        targetSearchRadius = Mathf.Max(0.1f, targetSearchRadius);
        buildingSearchRadius = Mathf.Max(targetSearchRadius, buildingSearchRadius);
        buildingPriorityRadius = Mathf.Clamp(buildingPriorityRadius, 0.1f, buildingSearchRadius);
        minionInterruptRadius = Mathf.Max(0.1f, minionInterruptRadius);

#if UNITY_EDITOR
        if (dogVictoryPrefab == null)
        {
            dogVictoryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DogVictoryPrefabPath);
        }

        if (catVictoryPrefab == null)
        {
            catVictoryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CatVictoryPrefabPath);
        }

        if (victoryResultPrefab == null)
        {
            victoryResultPrefab = AssetDatabase.LoadAssetAtPath<VictoryResultView>(VictoryResultPrefabPath);
        }
#endif
    }

    public int GetCost(MinionKind kind)
    {
        return kind == MinionKind.Melee ? meleeCost : rangedCost;
    }

    public string GetDisplayName(MinionKind kind)
    {
        return kind == MinionKind.Melee ? "\u8fd1\u6230\u5c0f\u5175" : "\u9060\u7a0b\u5c0f\u5175";
    }

    public string GetDescription(MinionKind kind)
    {
        if (kind == MinionKind.Melee)
        {
            return meleeHealth + " HP / " + meleeDamage + " ATK";
        }

        return rangedHealth + " HP / " + rangedDamage + " ATK / long range";
    }

    public bool TryBuyAndSummon(MinionKind kind, MinionTeam team = MinionTeam.Dog)
    {
        if (gameEnded)
        {
            return false;
        }

        ResourceManager resources = ResourceManager.Instance;
        int cost = GetCost(kind);
        if (resources == null || !resources.Spend(ResourceType.Coin, cost))
        {
            return false;
        }

        Summon(kind, team);
        return true;
    }

    public MinionUnit Summon(MinionKind kind, MinionTeam team)
    {
        if (gameEnded)
        {
            return null;
        }

        ResolveLanePoints();

        Transform spawn = GetSpawnPoint(team);
        Transform goal = GetGoalPoint(team);
        Vector3 position = GetSpawnPosition(spawn, goal, team);
        return CreateMinion(kind, team, position, goal, GetEnemyBase(team));
    }

    public MinionUnit SummonAt(MinionKind kind, MinionTeam team, Vector3 position)
    {
        if (gameEnded)
        {
            return null;
        }

        ResolveLanePoints();

        Transform goal = GetGoalPoint(team);
        Vector3 spawnPosition = ProjectToGround(position, null) + Vector3.up * spawnOffset.y;
        return CreateMinion(kind, team, spawnPosition, goal, GetEnemyBase(team));
    }

    public void SetSinglePlayerCatPrefab(GameObject prefab)
    {
        singlePlayerCatPrefab = prefab;
        if (singlePlayerCatStandIn != null && singlePlayerCatPrefab != null && singlePlayerCatStandIn.name == "AI Cat Stand-In")
        {
            Destroy(singlePlayerCatStandIn);
            singlePlayerCatStandIn = null;
            EnsureSinglePlayerCatStandIn();
        }
    }

    public void SetHumanCatOpponentPresent(bool present)
    {
        humanCatOpponentPresent = present;
        if (humanCatOpponentPresent)
        {
            DestroySinglePlayerCatStandIn();
        }
        else
        {
            EnsureSinglePlayerCatStandIn();
        }
    }

    public bool ShouldRunSinglePlayerCatAi()
    {
        return !humanCatOpponentPresent;
    }

    public void EnsureSinglePlayerCatStandIn()
    {
        if (!createSinglePlayerCatStandIn || humanCatOpponentPresent || singlePlayerCatStandIn != null)
        {
            return;
        }

        ResolveLanePoints();
        Transform spawn = GetSpawnPoint(MinionTeam.Cat);
        Vector3 position = GetStandInPosition(spawn);

        bool createdFromPrefab = singlePlayerCatPrefab != null;
        if (createdFromPrefab)
        {
            singlePlayerCatStandIn = Instantiate(singlePlayerCatPrefab, position, Quaternion.identity);
            singlePlayerCatStandIn.name = "AI Cat Stand-In Prefab";
            DisablePlayerControlScripts(singlePlayerCatStandIn);
        }
        else
        {
            singlePlayerCatStandIn = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            singlePlayerCatStandIn.name = "AI Cat Stand-In";
            singlePlayerCatStandIn.transform.position = position;
            singlePlayerCatStandIn.transform.localScale = new Vector3(0.8f, 1.1f, 0.8f);
        }

        AlignBottomToGround(singlePlayerCatStandIn);

        Renderer renderer = singlePlayerCatStandIn.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = catTeamMaterial;
        }

        Rigidbody body = singlePlayerCatStandIn.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = singlePlayerCatStandIn.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;

        if (!createdFromPrefab && singlePlayerCatStandIn.GetComponent<CatPlayerController>() == null)
        {
            singlePlayerCatStandIn.AddComponent<CatPlayerController>();
        }

        CreateStandInLabel(singlePlayerCatStandIn.transform);
    }

    private Vector3 GetStandInPosition(Transform spawn)
    {
        Vector3 position = spawn != null ? spawn.position : Vector3.zero;
        Vector3 grounded = ProjectToGround(position, spawn);
        return grounded + Vector3.up * spawnOffset.y;
    }

    public void DestroySinglePlayerCatStandIn()
    {
        if (singlePlayerCatStandIn != null)
        {
            Destroy(singlePlayerCatStandIn);
            singlePlayerCatStandIn = null;
        }
    }

    private MinionUnit CreateMinion(MinionKind kind, MinionTeam team, Vector3 position, Transform goal, MinionBaseHealth enemyBase)
    {
        GameObject visualPrefab = GetVisualPrefab(kind, team);
        bool useVisualPrefab = visualPrefab != null;
        PrimitiveType primitive = kind == MinionKind.Melee ? PrimitiveType.Capsule : PrimitiveType.Cylinder;
        GameObject minionObject = useVisualPrefab ? new GameObject(team + " " + kind + " Minion") : GameObject.CreatePrimitive(primitive);
        minionObject.name = team + " " + kind + " Minion";
        minionObject.transform.position = position;
        minionObject.transform.localScale = Vector3.one;

        if (useVisualPrefab)
        {
            CapsuleCollider capsule = minionObject.AddComponent<CapsuleCollider>();
            capsule.radius = kind == MinionKind.Melee ? 0.35f : 0.32f;
            capsule.height = kind == MinionKind.Melee ? 1.1f : 0.9f;
            capsule.center = Vector3.up * capsule.height * 0.5f;
            capsule.isTrigger = true;
        }
        else
        {
            minionObject.transform.localScale = kind == MinionKind.Melee
                ? new Vector3(0.7f, 1.1f, 0.7f)
                : new Vector3(0.65f, 0.9f, 0.65f);
        }

        AlignBottomToGround(minionObject);

        Collider minionCollider = minionObject.GetComponent<Collider>();
        if (minionCollider != null)
        {
            minionCollider.isTrigger = true;
        }

        Renderer renderer = minionObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = whiteModelMaterial;
            renderer.enabled = !useVisualPrefab;
        }

        if (useVisualPrefab)
        {
            GameObject visualObject = Instantiate(visualPrefab, minionObject.transform);
            visualObject.name = GetVisualName(kind, team);
            visualObject.transform.localPosition = Vector3.zero;
            visualObject.transform.localRotation = Quaternion.Euler(0f, GetVisualYawOffset(kind, team), 0f);
            visualObject.transform.localScale = Vector3.one;
            RemoveRuntimeComponentsFromVisual(visualObject);
            ApplyToonStyleToVisual(visualObject, kind, team);
            Vector3 alignedLocalPosition = AlignVisualBottomToRoot(visualObject.transform, minionObject);
            MinionVisualAnimator visualAnimator = minionObject.GetComponent<MinionVisualAnimator>();
            if (visualAnimator == null)
            {
                visualAnimator = minionObject.AddComponent<MinionVisualAnimator>();
            }

            visualAnimator.Initialize(visualObject.transform);
            visualAnimator.SetBaseLocalPosition(alignedLocalPosition);
        }

        Rigidbody body = minionObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        PlayerUpgradeManager upgrades = PlayerUpgradeManager.EnsureInstance();
        int effectiveHealth = GetEffectiveHealth(kind, team, upgrades);
        int effectiveDamage = GetEffectiveAttackDamage(kind, team, upgrades);
        int effectiveBuildingDamage = GetEffectiveBuildingDamage(kind, team, upgrades);
        float effectiveAttackRange = GetEffectiveAttackRange(kind);

        MinionCombatant combatant = minionObject.AddComponent<MinionCombatant>();
        combatant.Configure(team, effectiveHealth);

        MinionUnit unit = minionObject.AddComponent<MinionUnit>();
        unit.Configure(
            kind,
            goal,
            kind == MinionKind.Melee ? meleeMoveSpeed : rangedMoveSpeed,
            effectiveAttackRange,
            effectiveDamage,
            effectiveBuildingDamage,
            kind == MinionKind.Melee ? meleeAttackCooldown : rangedAttackCooldown,
            targetSearchRadius,
            buildingSearchRadius,
            buildingPriorityRadius,
            minionInterruptRadius,
            enemyBase);

        if (!useVisualPrefab)
        {
            CreateTeamMarker(minionObject.transform, team);
            if (kind == MinionKind.Ranged)
            {
                CreateRangedMarker(minionObject.transform);
            }
        }

        return unit;
    }

    private int GetEffectiveHealth(MinionKind kind, MinionTeam team, PlayerUpgradeManager upgrades)
    {
        int baseHealthValue = kind == MinionKind.Melee ? meleeHealth : rangedHealth;
        int bonus = 0;
        if (upgrades != null)
        {
            bonus = kind == MinionKind.Melee
                ? upgrades.GetMeleeTrainingHealthBonus(team)
                : upgrades.GetRangedTrainingHealthBonus(team);
        }

        return Mathf.Max(1, baseHealthValue + bonus);
    }

    private int GetEffectiveAttackDamage(MinionKind kind, MinionTeam team, PlayerUpgradeManager upgrades)
    {
        int baseDamage = kind == MinionKind.Melee ? meleeDamage : rangedDamage;
        int bonus = 0;
        if (upgrades != null)
        {
            bonus = kind == MinionKind.Melee
                ? upgrades.GetMeleeTrainingDamageBonus(team)
                : upgrades.GetRangedTrainingDamageBonus(team);
        }

        return Mathf.Max(1, baseDamage + bonus);
    }

    private int GetEffectiveBuildingDamage(MinionKind kind, MinionTeam team, PlayerUpgradeManager upgrades)
    {
        int baseDamage = kind == MinionKind.Melee ? meleeDamage : rangedDamage;
        int bonus = kind == MinionKind.Melee && upgrades != null ? upgrades.GetMeleeTrainingDamageBonus(team) : 0;
        return Mathf.Max(1, baseDamage + bonus);
    }

    private float GetEffectiveAttackRange(MinionKind kind)
    {
        return Mathf.Max(0.2f, kind == MinionKind.Melee ? meleeAttackRange : rangedAttackRange);
    }

    private GameObject GetVisualPrefab(MinionKind kind, MinionTeam team)
    {
        GameObject prefab = null;
        if (team == MinionTeam.Cat)
        {
            prefab = kind == MinionKind.Melee ? catMeleeVisualPrefab : catRangedVisualPrefab;
        }
        else if (team == MinionTeam.Dog)
        {
            prefab = kind == MinionKind.Melee ? dogMeleeVisualPrefab : dogRangedVisualPrefab;
        }

#if UNITY_EDITOR
        if (prefab == null)
        {
            string path = GetVisualFallbackPath(kind, team);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
#endif
        return prefab;
    }

    private float GetVisualYawOffset(MinionKind kind, MinionTeam team)
    {
        if (team == MinionTeam.Cat)
        {
            return kind == MinionKind.Melee ? catMeleeVisualYawOffset : catRangedVisualYawOffset;
        }

        return kind == MinionKind.Melee ? dogMeleeVisualYawOffset : dogRangedVisualYawOffset;
    }

    private static string GetVisualFallbackPath(MinionKind kind, MinionTeam team)
    {
        if (team == MinionTeam.Cat)
        {
            return kind == MinionKind.Melee ? CatMeleeVisualPath : CatRangedVisualPath;
        }

        return kind == MinionKind.Melee ? DogMeleeVisualPath : DogRangedVisualPath;
    }

    private static string GetVisualName(MinionKind kind, MinionTeam team)
    {
        string teamName = team == MinionTeam.Cat ? "cat" : "dog";
        string kindName = kind == MinionKind.Melee ? "melee" : "ranged";
        return teamName + "_" + kindName + " Visual";
    }

    private static Vector3 AlignVisualBottomToRoot(Transform visualRoot, GameObject minionObject)
    {
        Bounds bounds;
        if (!TryGetRendererBounds(visualRoot, out bounds))
        {
            return visualRoot.localPosition;
        }

        Collider rootCollider = minionObject.GetComponent<Collider>();
        float rootBottom = rootCollider != null ? rootCollider.bounds.min.y : minionObject.transform.position.y;
        Vector3 worldOffset = new Vector3(
            minionObject.transform.position.x - bounds.center.x,
            rootBottom - bounds.min.y - VisualGroundSink,
            minionObject.transform.position.z - bounds.center.z);
        visualRoot.localPosition += minionObject.transform.InverseTransformVector(worldOffset);
        return visualRoot.localPosition;
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(root.position, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.GetComponent<CharacterOutlineProxy>() != null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static void RemoveRuntimeComponentsFromVisual(GameObject visualObject)
    {
        Collider[] colliders = visualObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                Destroy(colliders[i]);
            }
        }

        Rigidbody[] bodies = visualObject.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null)
            {
                Destroy(bodies[i]);
            }
        }
    }

    private static void ApplyToonStyleToVisual(GameObject visualObject, MinionKind kind, MinionTeam team)
    {
        if (visualObject == null)
        {
            return;
        }

        bool isCatRanged = team == MinionTeam.Cat && kind == MinionKind.Ranged;
        Material outline = isCatRanged ? null : GetMinionOutlineMaterial();
        ToonCharacterSetup setup = visualObject.GetComponent<ToonCharacterSetup>();
        if (setup == null)
        {
            setup = visualObject.AddComponent<ToonCharacterSetup>();
            setup.Configure(visualObject.transform, outline, null, true, outline != null);
        }
        else if (isCatRanged)
        {
            setup.Configure(visualObject.transform, null, null, true, false);
        }
        else
        {
            setup.EnsureConfiguration(visualObject.transform, outline);
        }

        if (isCatRanged)
        {
            RemoveOutlineProxies(visualObject);
            ApplyCatRangedToonTuning(visualObject);
        }
    }

    private static Material GetMinionOutlineMaterial()
    {
        if (minionOutlineMaterial != null)
        {
            return minionOutlineMaterial;
        }

        Shader outlineShader = Shader.Find("Custom/URPToonOutline");
        if (outlineShader == null)
        {
            return null;
        }

        minionOutlineMaterial = new Material(outlineShader)
        {
            name = "Runtime Minion Toon Outline"
        };

        if (minionOutlineMaterial.HasProperty("_OutlineColor"))
        {
            minionOutlineMaterial.SetColor("_OutlineColor", new Color(0.14f, 0.08f, 0.06f, 1f));
        }

        if (minionOutlineMaterial.HasProperty("_OutlineWidth"))
        {
            minionOutlineMaterial.SetFloat("_OutlineWidth", MinionOutlineWidth);
        }

        return minionOutlineMaterial;
    }

    private static void RemoveOutlineProxies(GameObject visualObject)
    {
        CharacterOutlineProxy[] proxies = visualObject.GetComponentsInChildren<CharacterOutlineProxy>(true);
        for (int i = 0; i < proxies.Length; i++)
        {
            if (proxies[i] != null)
            {
                Destroy(proxies[i].gameObject);
            }
        }
    }

    private static void ApplyCatRangedToonTuning(GameObject visualObject)
    {
        Renderer[] renderers = visualObject.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.GetComponent<CharacterOutlineProxy>() != null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null)
                {
                    continue;
                }

                SetColorIfAvailable(material, "_Color", new Color(1.18f, 1.16f, 1.2f, 1f));
                SetColorIfAvailable(material, "_ShadowColor", new Color(0.18f, 0.16f, 0.2f, 1f));
                SetFloatIfAvailable(material, "_ShadowThreshold", 0.2f);
                SetFloatIfAvailable(material, "_ShadowSmoothness", 0.11f);
                SetColorIfAvailable(material, "_RimColor", new Color(0.72f, 0.66f, 0.9f, 1f));
                SetFloatIfAvailable(material, "_RimPower", 2.2f);
                SetFloatIfAvailable(material, "_RimStrength", 0.16f);
                SetColorIfAvailable(material, "_OutlineColor", new Color(0.08f, 0.06f, 0.08f, 1f));
                SetFloatIfAvailable(material, "_OutlineWidth", 0.003f);
            }
        }
    }

    private static void SetColorIfAvailable(Material material, string propertyName, Color value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static void SetFloatIfAvailable(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private void AlignBottomToGround(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Vector3 origin = target.transform.position + Vector3.up * 12f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
        bool foundGround = false;
        float bestDistance = float.PositiveInfinity;
        Vector3 bestPoint = target.transform.position;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(target.transform) || hits[i].normal.y < 0.5f)
            {
                continue;
            }

            if (hits[i].distance < bestDistance)
            {
                foundGround = true;
                bestDistance = hits[i].distance;
                bestPoint = hits[i].point;
            }
        }

        if (!foundGround)
        {
            return;
        }

        Collider selfCollider = target.GetComponent<Collider>();
        float centerToBottom = selfCollider != null ? target.transform.position.y - selfCollider.bounds.min.y : 0f;
        target.transform.position = new Vector3(target.transform.position.x, bestPoint.y + centerToBottom + 0.005f, target.transform.position.z);
    }

    private static void DisablePlayerControlScripts(GameObject target)
    {
        MonoBehaviour[] behaviours = target.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
            {
                behaviours[i].enabled = false;
            }
        }
    }

    private void CreateTeamMarker(Transform parent, MinionTeam team)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "Team Marker";
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        marker.transform.localScale = new Vector3(0.38f, 0.12f, 0.38f);

        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = GetTeamMaterial(team);
        }

        Collider collider = marker.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private void CreateStandInLabel(Transform parent)
    {
        WorldSpaceHealthLabel label = WorldSpaceHealthLabel.Create(
            parent,
            "AI Cat Label",
            new Vector3(0f, 1.5f, 0f),
            28,
            new Vector2(150f, 44f),
            0.01f);
        label.SetText("AI 醜貓");
    }

    private void CreateRangedMarker(Transform parent)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "Ranged Marker";
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = new Vector3(0.42f, 0.15f, 0f);
        marker.transform.localScale = new Vector3(0.12f, 0.8f, 0.12f);

        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = whiteModelMaterial;
        }

        Collider collider = marker.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private Transform GetSpawnPoint(MinionTeam team)
    {
        return team == MinionTeam.Dog ? dogSpawnPoint : catSpawnPoint;
    }

    private Transform GetGoalPoint(MinionTeam team)
    {
        return team == MinionTeam.Dog ? dogGoalPoint : catGoalPoint;
    }

    private MinionBaseHealth GetEnemyBase(MinionTeam team)
    {
        return team == MinionTeam.Dog ? catBase : dogBase;
    }

    private Vector3 GetSpawnPosition(Transform spawn, Transform goal, MinionTeam team)
    {
        Vector3 position = spawn != null ? spawn.position : Vector3.zero;
        Vector3 laneDirection = Vector3.forward;
        if (goal != null)
        {
            laneDirection = goal.position - position;
            laneDirection.y = 0f;
        }

        if (laneDirection.sqrMagnitude < 0.001f)
        {
            laneDirection = team == MinionTeam.Dog ? Vector3.forward : Vector3.back;
        }

        Vector3 sideOffset = Vector3.Cross(Vector3.up, laneDirection.normalized) * Random.Range(-0.8f, 0.8f);
        return ProjectToGround(position + laneDirection.normalized * spawnOffset.z + sideOffset, spawn) + Vector3.up * spawnOffset.y;
    }

    private void ResolveLanePoints()
    {
        if (dogSpawnPoint == null)
        {
            dogSpawnPoint = FindShopTransform(MinionTeam.Dog);
        }

        if (dogGoalPoint == null)
        {
            dogGoalPoint = FindShopTransform(MinionTeam.Cat);
        }

        if (dogGoalPoint == null)
        {
            dogGoalPoint = FindTransformByName("Red Camp Core");
        }

        if (dogGoalPoint == null)
        {
            dogGoalPoint = CreatePrototypeCatTarget();
        }

        if (catSpawnPoint == null)
        {
            catSpawnPoint = dogGoalPoint;
        }

        if (catGoalPoint == null)
        {
            catGoalPoint = dogSpawnPoint;
        }

        dogBase = EnsureBaseHealth(dogSpawnPoint, MinionTeam.Dog);
        catBase = EnsureBaseHealth(dogGoalPoint, MinionTeam.Cat);
    }

    private MinionBaseHealth EnsureBaseHealth(Transform target, MinionTeam team)
    {
        if (target == null)
        {
            return null;
        }

        MinionBaseHealth health = target.GetComponent<MinionBaseHealth>();
        if (health == null)
        {
            health = target.gameObject.AddComponent<MinionBaseHealth>();
            health.Configure(team, baseHealth);
        }

        health.Destroyed -= OnBaseDestroyed;
        health.Destroyed += OnBaseDestroyed;
        health.SetLabelOffset(baseHealthLabelOffset);
        return health;
    }

    private void OnBaseDestroyed(MinionBaseHealth destroyedBase)
    {
        if (destroyedBase == null || gameEnded)
        {
            return;
        }

        MinionTeam losingTeam = destroyedBase.Team;
        MinionTeam winningTeam = losingTeam == MinionTeam.Dog ? MinionTeam.Cat : MinionTeam.Dog;
        EndGame(winningTeam, losingTeam);
    }

    private void EndGame(MinionTeam winningTeam, MinionTeam losingTeam)
    {
        gameEnded = true;
        SettlementResultData.SetResult(
            winningTeam,
            losingTeam,
            Time.unscaledTime - matchStartUnscaledTime,
            GetVictoryPrefab(winningTeam));
        ShowResult(winningTeam, losingTeam);
        Time.timeScale = 0f;
    }

    private void ShowResult(MinionTeam winningTeam, MinionTeam losingTeam)
    {
        pendingWinningTeam = winningTeam;
        EnsureResultUi();
        ConfigureResultConfirmButton(LoadSettlementScene, "\u78ba\u8a8d");
        HideVictoryCharacter();

        string result = "\u6230\u9b25\u7d50\u675f\n\u919c\u540d\u5df2\u5b9a\uff0c\u6309\u4e0b\u78ba\u8a8d\u9032\u5165\u52a0\u5195\u7d50\u7b97";
        if (resultView != null)
        {
            resultView.ShowResult(result, null);
        }
        else if (resultText != null)
        {
            resultText.text = result;
        }

        if (resultCanvas != null)
        {
            resultCanvas.gameObject.SetActive(true);
        }
    }

    private void ShowWinnerShowcase()
    {
        ConfigureResultConfirmButton(ReturnToMainMenu, "\u8fd4\u56de\u4e3b\u9078\u55ae");
        string result = GetTeamName(pendingWinningTeam);

        if (resultView != null)
        {
            resultView.ShowResult(result, null);
        }
        else if (resultText != null)
        {
            resultText.text = result;
        }

        ShowVictoryCharacter(pendingWinningTeam);

        if (resultView != null)
        {
            resultView.SetCharacterTexture(resultCharacterTexture);
        }

        if (resultCanvas != null)
        {
            resultCanvas.gameObject.SetActive(true);
        }
    }

    private void EnsureResultUi()
    {
        if (resultCanvas != null && resultText != null)
        {
            return;
        }

        EnsureEventSystem();

        if (victoryResultPrefab != null)
        {
            resultView = Instantiate(victoryResultPrefab);
            resultView.name = "Match Result Canvas";
            resultView.BindIfNeeded();
            resultCanvas = resultView.GetComponent<Canvas>();
            resultText = resultView.ResultText;
            resultCharacterImage = resultView.CharacterImage;
            resultConfirmButton = null;
            resultConfirmButtonText = null;
            resultView.gameObject.SetActive(false);
            return;
        }

#if UNITY_EDITOR
        victoryResultPrefab = AssetDatabase.LoadAssetAtPath<VictoryResultView>(VictoryResultPrefabPath);
        if (victoryResultPrefab != null)
        {
            EnsureResultUi();
            return;
        }
#endif

        GameObject canvasObject = new GameObject("Match Result Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        resultCanvas = canvasObject.GetComponent<Canvas>();
        resultCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        resultCanvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject shadeObject = new GameObject("Result Shade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        shadeObject.transform.SetParent(canvasObject.transform, false);
        RectTransform shadeRect = shadeObject.GetComponent<RectTransform>();
        shadeRect.anchorMin = Vector2.zero;
        shadeRect.anchorMax = Vector2.one;
        shadeRect.offsetMin = Vector2.zero;
        shadeRect.offsetMax = Vector2.zero;

        Image shade = shadeObject.GetComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.62f);
        shade.raycastTarget = true;

        GameObject textObject = new GameObject("Result Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        textObject.transform.SetParent(shadeObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.12f, 0.62f);
        textRect.anchorMax = new Vector2(0.88f, 0.84f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        resultText = textObject.GetComponent<Text>();
        resultText.font = LoadReadableFont();
        resultText.alignment = TextAnchor.MiddleCenter;
        resultText.fontSize = 78;
        resultText.fontStyle = FontStyle.Bold;
        resultText.horizontalOverflow = HorizontalWrapMode.Wrap;
        resultText.verticalOverflow = VerticalWrapMode.Overflow;
        resultText.raycastTarget = false;

        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(4f, -4f);

        GameObject characterObject = new GameObject("Victory Character Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        characterObject.transform.SetParent(shadeObject.transform, false);
        RectTransform characterRect = characterObject.GetComponent<RectTransform>();
        characterRect.anchorMin = new Vector2(0.18f, 0.22f);
        characterRect.anchorMax = new Vector2(0.82f, 0.61f);
        characterRect.offsetMin = Vector2.zero;
        characterRect.offsetMax = Vector2.zero;

        resultCharacterImage = characterObject.GetComponent<RawImage>();
        resultCharacterImage.color = Color.white;
        resultCharacterImage.raycastTarget = false;

        GameObject buttonsObject = new GameObject("Result Buttons", typeof(RectTransform));
        buttonsObject.transform.SetParent(shadeObject.transform, false);
        RectTransform buttonsRect = buttonsObject.GetComponent<RectTransform>();
        buttonsRect.anchorMin = new Vector2(0.28f, 0.12f);
        buttonsRect.anchorMax = new Vector2(0.72f, 0.22f);
        buttonsRect.offsetMin = Vector2.zero;
        buttonsRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup layout = buttonsObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.spacing = 28f;

        resultConfirmButton = CreateResultButton(buttonsObject.transform, "\u78ba\u8a8d", ReturnToMainMenu);
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private Button CreateResultButton(Transform parent, string label, UnityAction action)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.92f);

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(action);

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 280f;
        layout.preferredHeight = 74f;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObject.GetComponent<Text>();
        text.font = LoadReadableFont();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 34;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.raycastTarget = false;
        resultConfirmButtonText = text;
        return button;
    }

    private void ConfigureResultConfirmButton(UnityAction action, string label)
    {
        if (resultView != null)
        {
            resultView.SetConfirmAction(action);
            resultView.SetConfirmButtonLabel(label);
        }

        if (resultConfirmButton != null)
        {
            resultConfirmButton.onClick.RemoveAllListeners();
            resultConfirmButton.onClick.AddListener(action);
        }

        if (resultConfirmButtonText != null)
        {
            resultConfirmButtonText.text = label;
        }
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void LoadSettlementScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SettlementSceneName);
    }

    private void ShowVictoryCharacter(MinionTeam winningTeam)
    {
        EnsureResultCharacterStage();

        if (resultCharacterImage == null || resultCharacterTexture == null)
        {
            return;
        }

        if (resultCharacterInstance != null)
        {
            Destroy(resultCharacterInstance);
            resultCharacterInstance = null;
        }

        GameObject prefab = GetVictoryPrefab(winningTeam);
        if (prefab == null)
        {
            resultCharacterImage.enabled = false;
            return;
        }

        resultCharacterInstance = Instantiate(prefab, resultCharacterStage.transform);
        resultCharacterInstance.name = GetTeamName(winningTeam) + " Victory Character";
        resultCharacterInstance.transform.localPosition = Vector3.zero;
        resultCharacterInstance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        resultCharacterInstance.transform.localScale = Vector3.one;
        ApplyLayerRecursively(resultCharacterInstance, VictoryPreviewLayer);
        PrepareVictoryCharacter(resultCharacterInstance);
        FitVictoryCharacter(resultCharacterInstance.transform);
        VictoryCharacterPreviewMotion motion = resultCharacterInstance.AddComponent<VictoryCharacterPreviewMotion>();
        motion.Configure(GetVictoryActionState(winningTeam));

        resultCharacterImage.texture = resultCharacterTexture;
        resultCharacterImage.enabled = true;
    }

    private void HideVictoryCharacter()
    {
        if (resultCharacterInstance != null)
        {
            Destroy(resultCharacterInstance);
            resultCharacterInstance = null;
        }

        if (resultCharacterImage != null)
        {
            resultCharacterImage.texture = null;
            resultCharacterImage.enabled = false;
        }

        if (resultView != null)
        {
            resultView.SetCharacterTexture(null);
        }
    }

    private void EnsureResultCharacterStage()
    {
        if (resultCharacterStage != null && resultCharacterTexture != null)
        {
            return;
        }

        DestroyResultCharacterStage();

        resultCharacterTexture = new RenderTexture(720, 520, 16, RenderTextureFormat.ARGB32)
        {
            name = "Victory Character Render Texture",
            antiAliasing = 4
        };

        resultCharacterStage = new GameObject("Victory Character Stage");
        resultCharacterStage.layer = VictoryPreviewLayer;
        resultCharacterStage.transform.position = new Vector3(0f, -200f, 0f);

        GameObject cameraObject = new GameObject("Victory Character Camera", typeof(Camera));
        cameraObject.layer = VictoryPreviewLayer;
        cameraObject.transform.SetParent(resultCharacterStage.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.35f, -4.2f);
        cameraObject.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);

        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.cullingMask = VictoryPreviewMask;
        camera.targetTexture = resultCharacterTexture;
        camera.fieldOfView = 32f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 30f;
        camera.allowHDR = false;
        camera.allowMSAA = true;

        GameObject lightObject = new GameObject("Victory Character Light", typeof(Light));
        lightObject.layer = VictoryPreviewLayer;
        lightObject.transform.SetParent(resultCharacterStage.transform, false);
        lightObject.transform.localPosition = new Vector3(-1.8f, 3.1f, -2.6f);
        lightObject.transform.localRotation = Quaternion.Euler(52f, 28f, 0f);

        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.35f;
        light.cullingMask = VictoryPreviewMask;
    }

    private void DestroyResultCharacterStage()
    {
        if (resultCharacterTexture != null)
        {
            resultCharacterTexture.Release();
            Destroy(resultCharacterTexture);
            resultCharacterTexture = null;
        }

        if (resultCharacterStage != null)
        {
            Destroy(resultCharacterStage);
            resultCharacterStage = null;
        }

        resultCharacterInstance = null;
    }

    private GameObject GetVictoryPrefab(MinionTeam team)
    {
        GameObject prefab = team == MinionTeam.Dog ? dogVictoryPrefab : catVictoryPrefab;
        if (prefab != null)
        {
            return prefab;
        }

#if UNITY_EDITOR
        string path = team == MinionTeam.Dog ? DogVictoryPrefabPath : CatVictoryPrefabPath;
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
        return null;
#endif
    }

    private static void PrepareVictoryCharacter(GameObject character)
    {
        if (character == null)
        {
            return;
        }

        Collider[] colliders = character.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] bodies = character.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].isKinematic = true;
            bodies[i].useGravity = false;
        }

        MonoBehaviour[] behaviours = character.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
            {
                behaviours[i].enabled = false;
            }
        }
    }

    private static void ApplyLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
        {
            return;
        }

        Transform[] transforms = target.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            transforms[i].gameObject.layer = layer;
        }
    }

    private static void FitVictoryCharacter(Transform character)
    {
        if (character == null || !TryGetRendererBounds(character, out Bounds bounds))
        {
            return;
        }

        float height = Mathf.Max(0.1f, bounds.size.y);
        float scale = 2.2f / height;
        character.localScale *= scale;

        if (!TryGetRendererBounds(character, out bounds))
        {
            return;
        }

        Vector3 offset = -bounds.center;
        offset.y += bounds.extents.y;
        character.position += offset;
    }

    private static string GetTeamName(MinionTeam team)
    {
        return team == MinionTeam.Dog ? "\u919c\u72d7" : "\u919c\u8c93";
    }

    private static string GetVictoryActionState(MinionTeam team)
    {
        return "Attack";
    }

    private static Font LoadReadableFont()
    {
        return UglyDogUIFont.Load();
    }

    private Transform CreatePrototypeCatTarget()
    {
        Vector3 position = new Vector3(18f, 0.1f, 0f);
        if (dogSpawnPoint != null)
        {
            position = dogSpawnPoint.position;
            position.x = Mathf.Abs(position.x) > 1f ? -position.x : position.x + 22f;
        }

        GameObject target = new GameObject("CatShopRange");
        target.transform.position = position;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "Prototype Cat Range Marker";
        visual.transform.SetParent(target.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(2.2f, 0.04f, 2.2f);

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = catTeamMaterial;
        }

        Collider collider = visual.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        return target.transform;
    }

    private Vector3 ProjectToGround(Vector3 position, Transform ignoredRoot)
    {
        Vector3 origin = position + Vector3.up * 12f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
        bool foundGround = false;
        float bestDistance = float.PositiveInfinity;
        Vector3 bestPoint = position;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hits[i].normal.y < 0.5f)
            {
                continue;
            }

            if (ignoredRoot != null
                && (hitCollider.transform.IsChildOf(ignoredRoot) || ignoredRoot.IsChildOf(hitCollider.transform)))
            {
                continue;
            }

            if (hits[i].distance < bestDistance)
            {
                foundGround = true;
                bestDistance = hits[i].distance;
                bestPoint = hits[i].point;
            }
        }

        return foundGround ? bestPoint : position;
    }

    private static Transform FindShopTransform(MinionTeam team)
    {
        string teamName = team == MinionTeam.Dog ? "dog" : "cat";
        UpgradeShopZone[] zones = FindObjectsOfType<UpgradeShopZone>(true);
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null && GetHierarchyName(zones[i].transform).ToLowerInvariant().Contains(teamName))
            {
                return zones[i].transform;
            }
        }

        if (team == MinionTeam.Dog)
        {
            return FindFirstTransformByName("DogShopRange", "dogShop", "DogShop");
        }

        return FindFirstTransformByName("CatShopRange", "catShopRange", "catShop", "CatShop");
    }

    private static Transform FindFirstTransformByName(params string[] objectNames)
    {
        for (int i = 0; i < objectNames.Length; i++)
        {
            Transform found = FindTransformByName(objectNames[i]);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindTransformByName(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }

    private static string GetHierarchyName(Transform target)
    {
        string names = string.Empty;
        Transform current = target;
        while (current != null)
        {
            names += " " + current.name;
            current = current.parent;
        }

        return names;
    }

    private static void EnsureSharedMaterials()
    {
        if (whiteModelMaterial == null)
        {
            whiteModelMaterial = CreateMaterial("Prototype Minion White", new Color(0.92f, 0.9f, 0.84f, 1f));
        }

        if (dogTeamMaterial == null)
        {
            dogTeamMaterial = CreateMaterial("Prototype Dog Team", new Color(0.3f, 0.55f, 1f, 1f));
        }

        if (catTeamMaterial == null)
        {
            catTeamMaterial = CreateMaterial("Prototype Cat Team", new Color(1f, 0.34f, 0.34f, 1f));
        }
    }

    private static Material CreateMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;
        material.SetColor("_BaseColor", color);
        return material;
    }
}
