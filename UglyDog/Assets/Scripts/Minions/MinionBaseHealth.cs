using System;
using UnityEngine;

public class MinionBaseHealth : MonoBehaviour
{
    [SerializeField] private MinionTeam team = MinionTeam.Dog;
    [SerializeField] private int maxHealth = 20;
    [SerializeField] private int currentHealth = 20;
    [SerializeField] private Vector3 labelOffset = new Vector3(0f, 2.2f, 0f);

    private WorldSpaceHealthLabel healthLabel;

    public MinionTeam Team => team;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDestroyed => currentHealth <= 0;

    public event Action<MinionBaseHealth> Destroyed;
    public event Action<MinionBaseHealth> HealthChanged;

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
            RefreshLabel();
        }
    }

    private void EnsureHealthLabel()
    {
        if (healthLabel != null)
        {
            return;
        }

        healthLabel = WorldSpaceHealthLabel.Create(
            transform,
            "Base Health Label",
            labelOffset,
            32,
            new Vector2(220f, 54f),
            0.012f);
    }

    private void RefreshLabel()
    {
        if (healthLabel != null)
        {
            healthLabel.SetText(currentHealth + "/" + maxHealth + " HP");
        }
    }
}
