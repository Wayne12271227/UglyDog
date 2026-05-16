using System;
using UnityEngine;

public class MinionCombatant : MonoBehaviour
{
    [SerializeField] private MinionTeam team = MinionTeam.Dog;
    [SerializeField] private int maxHealth = 20;
    [SerializeField] private int currentHealth = 20;
    [SerializeField] private Vector3 labelOffset = new Vector3(0f, 1.6f, 0f);

    private TextMesh healthLabel;

    public MinionTeam Team => team;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0;

    public event Action<MinionCombatant> Died;
    public event Action<MinionCombatant> HealthChanged;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 0, maxHealth);
        EnsureHealthLabel();
        RefreshLabel();
    }

    private void LateUpdate()
    {
        if (healthLabel == null)
        {
            return;
        }

        healthLabel.transform.position = transform.TransformPoint(labelOffset);
        Camera camera = Camera.main;
        if (camera != null)
        {
            Vector3 direction = healthLabel.transform.position - camera.transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                healthLabel.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    public void Configure(MinionTeam newTeam, int health)
    {
        team = newTeam;
        maxHealth = Mathf.Max(1, health);
        currentHealth = maxHealth;
        EnsureHealthLabel();
        RefreshLabel();
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsDead)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        HealthChanged?.Invoke(this);
        RefreshLabel();

        if (currentHealth <= 0)
        {
            Died?.Invoke(this);
            Destroy(gameObject);
        }
    }

    private void EnsureHealthLabel()
    {
        if (healthLabel != null)
        {
            return;
        }

        GameObject labelObject = new GameObject("Minion Health Label");
        labelObject.transform.SetParent(transform, false);
        healthLabel = labelObject.AddComponent<TextMesh>();
        healthLabel.anchor = TextAnchor.MiddleCenter;
        healthLabel.alignment = TextAlignment.Center;
        healthLabel.characterSize = 0.09f;
        healthLabel.fontSize = 24;
        healthLabel.color = Color.white;
    }

    private void RefreshLabel()
    {
        if (healthLabel != null)
        {
            healthLabel.text = currentHealth + "/" + maxHealth;
        }
    }
}
