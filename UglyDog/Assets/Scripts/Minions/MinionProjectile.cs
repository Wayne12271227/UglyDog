using UnityEngine;

public class MinionProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifeTime = 2f;

    private MinionCombatant target;
    private MinionBaseHealth baseTarget;
    private int damage;
    private float expireTime;

    public static void Spawn(Vector3 position, MinionCombatant target, int damage, MinionTeam sourceTeam)
    {
        if (target == null || target.IsDead)
        {
            return;
        }

        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = sourceTeam + " Minion Projectile";
        projectileObject.transform.position = position;
        projectileObject.transform.localScale = Vector3.one * 0.24f;

        Collider collider = projectileObject.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        Renderer renderer = projectileObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = MinionManager.GetTeamMaterial(sourceTeam);
        }

        MinionProjectile projectile = projectileObject.AddComponent<MinionProjectile>();
        projectile.Initialize(target, damage);
    }

    public static void Spawn(Vector3 position, MinionBaseHealth target, int damage, MinionTeam sourceTeam)
    {
        if (target == null || target.IsDestroyed)
        {
            return;
        }

        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = sourceTeam + " Minion Base Projectile";
        projectileObject.transform.position = position;
        projectileObject.transform.localScale = Vector3.one * 0.24f;

        Collider collider = projectileObject.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        Renderer renderer = projectileObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = MinionManager.GetTeamMaterial(sourceTeam);
        }

        MinionProjectile projectile = projectileObject.AddComponent<MinionProjectile>();
        projectile.Initialize(target, damage);
    }

    private void Initialize(MinionCombatant newTarget, int newDamage)
    {
        target = newTarget;
        damage = Mathf.Max(1, newDamage);
        expireTime = Time.time + lifeTime;
    }

    private void Initialize(MinionBaseHealth newTarget, int newDamage)
    {
        baseTarget = newTarget;
        damage = Mathf.Max(1, newDamage);
        expireTime = Time.time + lifeTime;
    }

    private void Update()
    {
        if (Time.time >= expireTime || !HasValidTarget())
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPosition = GetTargetPosition();
        Vector3 offset = targetPosition - transform.position;
        float step = speed * Time.deltaTime;
        if (offset.magnitude <= step)
        {
            ApplyDamage();
            Destroy(gameObject);
            return;
        }

        transform.position += offset.normalized * step;
    }

    private bool HasValidTarget()
    {
        if (target != null)
        {
            return !target.IsDead;
        }

        return baseTarget != null && !baseTarget.IsDestroyed;
    }

    private Vector3 GetTargetPosition()
    {
        if (target != null)
        {
            return target.transform.position + Vector3.up * 0.8f;
        }

        return baseTarget.transform.position + Vector3.up * 1f;
    }

    private void ApplyDamage()
    {
        if (target != null)
        {
            target.TakeDamage(damage);
        }
        else if (baseTarget != null)
        {
            baseTarget.TakeDamage(damage);
        }
    }
}
