using System;
using UnityEngine;

public class BuildingHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 20;
    [SerializeField] private int currentHealth = 20;
    [SerializeField] private Vector3 labelOffset = new Vector3(0f, 2.4f, 0f);

    private WorldSpaceHealthLabel healthLabel;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDestroyed => currentHealth <= 0;

    public event Action<BuildingHealth> Destroyed;
    public event Action<BuildingHealth> HealthChanged;

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

        healthLabel.transform.localPosition = labelOffset;
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    public void Configure(int health)
    {
        maxHealth = Mathf.Max(1, health);
        currentHealth = maxHealth;
        EnsureHealthLabel();
        RefreshLabel();
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsDestroyed)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        HealthChanged?.Invoke(this);
        RefreshLabel();

        if (currentHealth <= 0)
        {
            Destroyed?.Invoke(this);
            Destroy(gameObject);
        }
    }

    public void Repair(int amount)
    {
        if (amount <= 0 || IsDestroyed)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        HealthChanged?.Invoke(this);
        RefreshLabel();
    }

    private void EnsureHealthLabel()
    {
        if (healthLabel != null)
        {
            return;
        }

        healthLabel = WorldSpaceHealthLabel.Create(
            transform,
            "Building Health Label",
            labelOffset,
            28,
            new Vector2(220f, 50f),
            0.012f);
    }

    private void RefreshLabel()
    {
        if (healthLabel != null)
        {
            healthLabel.SetText($"{currentHealth}/{maxHealth} HP");
        }
    }
}
