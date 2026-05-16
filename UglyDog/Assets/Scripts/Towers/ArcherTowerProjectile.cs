using UnityEngine;

public class ArcherTowerProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 12f;
    [SerializeField] private float lifeTime = 3f;

    private MinionCombatant target;
    private int damage;
    private float expireTime;

    public static void Spawn(Vector3 position, MinionCombatant target, int damage, float speed, MinionTeam sourceTeam)
    {
        if (target == null || target.IsDead)
        {
            return;
        }

        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        projectileObject.name = sourceTeam + " Archer Tower Arrow";
        projectileObject.transform.position = position;
        projectileObject.transform.localScale = new Vector3(0.06f, 0.38f, 0.06f);

        Collider collider = projectileObject.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        Renderer renderer = projectileObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateProjectileMaterial();
        }

        ArcherTowerProjectile projectile = projectileObject.AddComponent<ArcherTowerProjectile>();
        projectile.Initialize(target, damage, speed);
    }

    private void Initialize(MinionCombatant newTarget, int newDamage, float newSpeed)
    {
        target = newTarget;
        damage = Mathf.Max(1, newDamage);
        speed = Mathf.Max(0.5f, newSpeed);
        expireTime = Time.time + lifeTime;
    }

    private void Update()
    {
        if (Time.time >= expireTime || target == null || target.IsDead)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPosition = target.transform.position + Vector3.up * 0.8f;
        Vector3 offset = targetPosition - transform.position;
        float step = speed * Time.deltaTime;

        if (offset.magnitude <= step)
        {
            target.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        transform.position += offset.normalized * step;
        transform.rotation = Quaternion.LookRotation(offset.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
    }

    private static Material CreateProjectileMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = "Archer Tower Arrow Material";
        material.color = Color.white;
        material.SetColor("_BaseColor", Color.white);
        return material;
    }
}
