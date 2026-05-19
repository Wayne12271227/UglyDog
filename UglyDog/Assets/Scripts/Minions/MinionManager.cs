using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MinionManager : MonoBehaviour
{
    public static MinionManager Instance { get; private set; }

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
    [SerializeField] private float meleeMoveSpeed = 2.6f;

    [Header("Ranged")]
    [SerializeField] private int rangedHealth = 20;
    [SerializeField] private int rangedDamage = 4;
    [SerializeField] private float rangedAttackRange = 5.5f;
    [SerializeField] private float rangedAttackCooldown = 1.25f;
    [SerializeField] private float rangedMoveSpeed = 2.2f;

    [Header("Search")]
    [SerializeField] private float targetSearchRadius = 7f;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.1f, 1.6f);
    [SerializeField] private int baseHealth = 20;
    [SerializeField] private bool createSinglePlayerCatStandIn = true;

    [Header("Cat Minion Prefabs")]
    [SerializeField] private GameObject catMeleeVisualPrefab;
    [SerializeField] private GameObject catRangedVisualPrefab;
    [SerializeField] private float catMeleeVisualYawOffset = 180f;
    [SerializeField] private float catRangedVisualYawOffset = 180f;

    private static Material whiteModelMaterial;
    private static Material dogTeamMaterial;
    private static Material catTeamMaterial;
    private MinionBaseHealth dogBase;
    private MinionBaseHealth catBase;
    private GameObject singlePlayerCatStandIn;
    private GameObject singlePlayerCatPrefab;
    private bool humanCatOpponentPresent;

    private const string CatMeleeVisualPath = "Assets/prefab/cat_melee.prefab";
    private const string CatRangedVisualPath = "Assets/prefab/cat_ranged.prefab";

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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureSharedMaterials();
        ResolveLanePoints();
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
        ResolveLanePoints();

        Transform spawn = GetSpawnPoint(team);
        Transform goal = GetGoalPoint(team);
        Vector3 position = GetSpawnPosition(spawn, goal, team);
        return CreateMinion(kind, team, position, goal, GetEnemyBase(team));
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
            visualObject.name = kind == MinionKind.Melee ? "cat_melee Visual" : "cat_ranged Visual";
            visualObject.transform.localPosition = Vector3.zero;
            visualObject.transform.localRotation = Quaternion.Euler(0f, GetVisualYawOffset(kind, team), 0f);
            visualObject.transform.localScale = Vector3.one;
            RemoveRuntimeComponentsFromVisual(visualObject);
            AlignVisualBottomToRoot(visualObject.transform, minionObject);
        }

        Rigidbody body = minionObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        MinionCombatant combatant = minionObject.AddComponent<MinionCombatant>();
        combatant.Configure(team, kind == MinionKind.Melee ? meleeHealth : rangedHealth);

        MinionUnit unit = minionObject.AddComponent<MinionUnit>();
        unit.Configure(
            kind,
            goal,
            kind == MinionKind.Melee ? meleeMoveSpeed : rangedMoveSpeed,
            kind == MinionKind.Melee ? meleeAttackRange : rangedAttackRange,
            kind == MinionKind.Melee ? meleeDamage : rangedDamage,
            kind == MinionKind.Melee ? meleeAttackCooldown : rangedAttackCooldown,
            targetSearchRadius,
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

    private GameObject GetVisualPrefab(MinionKind kind, MinionTeam team)
    {
        if (team != MinionTeam.Cat)
        {
            return null;
        }

        GameObject prefab = kind == MinionKind.Melee ? catMeleeVisualPrefab : catRangedVisualPrefab;
#if UNITY_EDITOR
        if (prefab == null)
        {
            string path = kind == MinionKind.Melee ? CatMeleeVisualPath : CatRangedVisualPath;
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
#endif
        return prefab;
    }

    private float GetVisualYawOffset(MinionKind kind, MinionTeam team)
    {
        if (team != MinionTeam.Cat)
        {
            return 0f;
        }

        return kind == MinionKind.Melee ? catMeleeVisualYawOffset : catRangedVisualYawOffset;
    }

    private static void AlignVisualBottomToRoot(Transform visualRoot, GameObject minionObject)
    {
        Bounds bounds;
        if (!TryGetRendererBounds(visualRoot, out bounds))
        {
            return;
        }

        Collider rootCollider = minionObject.GetComponent<Collider>();
        float rootBottom = rootCollider != null ? rootCollider.bounds.min.y : minionObject.transform.position.y;
        Vector3 worldOffset = new Vector3(
            minionObject.transform.position.x - bounds.center.x,
            rootBottom - bounds.min.y,
            minionObject.transform.position.z - bounds.center.z);
        visualRoot.localPosition += minionObject.transform.InverseTransformVector(worldOffset);
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(root.position, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
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
        target.transform.position = new Vector3(target.transform.position.x, bestPoint.y + centerToBottom + 0.02f, target.transform.position.z);
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
        label.SetText("AI CAT");
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

        return health;
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
