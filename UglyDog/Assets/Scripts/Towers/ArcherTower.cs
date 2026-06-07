using UnityEngine;

public class ArcherTower : MonoBehaviour
{
    [Header("Team")]
    [SerializeField] private MinionTeam ownerTeam = MinionTeam.Dog;

    [Header("Combat")]
    [SerializeField] private float attackRange = 8f;
    [SerializeField] private float shotsPerSecond = 1.1f;
    [SerializeField] private int damage = 6;
    [SerializeField] private float projectileSpeed = 13f;
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField] private Vector3 muzzleLocalPosition = new Vector3(0f, 2.2f, 0.35f);

    [Header("White Model")]
    [SerializeField] private bool createWhiteModelOnAwake = false;
    [SerializeField] private Color modelColor = Color.white;

    private Transform head;
    private Transform muzzle;
    private MinionCombatant target;
    private float nextShotTime;
    private float nextTargetScanTime;

    public MinionTeam OwnerTeam => ownerTeam;

    private void Awake()
    {
        if (createWhiteModelOnAwake)
        {
            EnsureWhiteModel();
        }
    }

    private void Update()
    {
        if (!IsValidTarget(target) || Time.time >= nextTargetScanTime)
        {
            target = FindTarget();
            nextTargetScanTime = Time.time + 0.2f;
        }

        if (target == null)
        {
            return;
        }

        AimAt(target.transform.position + Vector3.up * 0.8f);

        if (Time.time >= nextShotTime)
        {
            Shoot(target);
            nextShotTime = Time.time + 1f / Mathf.Max(0.05f, shotsPerSecond);
        }
    }

    public void Configure(MinionTeam newOwnerTeam, float newAttackRange, float newShotsPerSecond, int newDamage, float newProjectileSpeed)
    {
        ownerTeam = newOwnerTeam;
        attackRange = Mathf.Max(0.5f, newAttackRange);
        shotsPerSecond = Mathf.Max(0.05f, newShotsPerSecond);
        damage = Mathf.Max(1, newDamage);
        projectileSpeed = Mathf.Max(0.5f, newProjectileSpeed);
    }

    private MinionCombatant FindTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange, targetLayers, QueryTriggerInteraction.Collide);
        MinionCombatant bestTarget = null;
        float bestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            MinionCombatant candidate = hit != null ? hit.GetComponentInParent<MinionCombatant>() : null;
            if (!IsValidTarget(candidate))
            {
                continue;
            }

            float distance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private bool IsValidTarget(MinionCombatant candidate)
    {
        return candidate != null
            && !candidate.IsDead
            && candidate.Team != ownerTeam
            && (candidate.transform.position - transform.position).sqrMagnitude <= attackRange * attackRange;
    }

    private void AimAt(Vector3 worldPosition)
    {
        Transform aimRoot = head != null ? head : transform;
        Vector3 direction = worldPosition - aimRoot.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            aimRoot.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }

    private void Shoot(MinionCombatant currentTarget)
    {
        Vector3 spawnPosition = muzzle != null ? muzzle.position : transform.TransformPoint(muzzleLocalPosition);
        ArcherTowerProjectile.Spawn(spawnPosition, currentTarget, damage, projectileSpeed, ownerTeam);
    }

    private void EnsureWhiteModel()
    {
        if (transform.Find("White Model") != null)
        {
            return;
        }

        GameObject visualRoot = new GameObject("White Model");
        visualRoot.transform.SetParent(transform, false);

        Material material = CreateModelMaterial();
        CreatePrimitive(visualRoot.transform, PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.15f, 0f), new Vector3(1.4f, 0.3f, 1.4f), material);
        CreatePrimitive(visualRoot.transform, PrimitiveType.Cylinder, "Tower Body", new Vector3(0f, 1f, 0f), new Vector3(0.72f, 1.6f, 0.72f), material);
        CreatePrimitive(visualRoot.transform, PrimitiveType.Cube, "Platform", new Vector3(0f, 1.85f, 0f), new Vector3(1.35f, 0.28f, 1.35f), material);

        GameObject headObject = CreatePrimitive(visualRoot.transform, PrimitiveType.Cube, "Head", new Vector3(0f, 2.2f, 0f), new Vector3(0.9f, 0.35f, 0.65f), material);
        head = headObject.transform;

        GameObject bowObject = CreatePrimitive(head, PrimitiveType.Cube, "Bow Placeholder", new Vector3(0f, 0f, 0.45f), new Vector3(1.15f, 0.12f, 0.12f), material);
        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(head, false);
        muzzle.localPosition = new Vector3(0f, 0f, 0.65f);
    }

    private Material CreateModelMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = "Archer Tower White Model";
        material.color = modelColor;
        material.SetColor("_BaseColor", modelColor);
        return material;
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
