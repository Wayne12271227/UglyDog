using System;
using UnityEngine;

public class PlayerCombatant : MonoBehaviour
{
    [SerializeField] private int maxHealth = 20;
    [SerializeField] private int currentHealth = 20;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDefeated => currentHealth <= 0;

    public event Action<PlayerCombatant> HealthChanged;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 0, maxHealth);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsDefeated)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        HealthChanged?.Invoke(this);
    }
}
