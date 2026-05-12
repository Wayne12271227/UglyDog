using System;
using UnityEngine;

public enum PlayerUpgradeType
{
    MoveSpeed,
    WoodGatherSpeed,
    StoneGatherSpeed
}

public class PlayerUpgradeManager : MonoBehaviour
{
    public static PlayerUpgradeManager Instance { get; private set; }

    [Header("Levels")]
    [SerializeField] private int moveSpeedLevel;
    [SerializeField] private int woodGatherSpeedLevel;
    [SerializeField] private int stoneGatherSpeedLevel;

    [Header("Upgrade Limits")]
    [SerializeField] private int maxLevel = 5;

    [Header("Bonus Per Level")]
    [SerializeField] private float moveSpeedBonusPerLevel = 0.1f;
    [SerializeField] private float gatherSpeedBonusPerLevel = 0.15f;

    [Header("Costs")]
    [SerializeField] private int[] moveSpeedCosts = { 50, 100, 175, 275, 400 };
    [SerializeField] private int[] woodGatherSpeedCosts = { 40, 90, 160, 250, 375 };
    [SerializeField] private int[] stoneGatherSpeedCosts = { 60, 120, 200, 320, 480 };

    public event Action UpgradesChanged;

    public float MoveSpeedMultiplier => 1f + moveSpeedLevel * moveSpeedBonusPerLevel;
    public float WoodGatherSpeedMultiplier => 1f + woodGatherSpeedLevel * gatherSpeedBonusPerLevel;
    public float StoneGatherSpeedMultiplier => 1f + stoneGatherSpeedLevel * gatherSpeedBonusPerLevel;

    public static PlayerUpgradeManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        PlayerUpgradeManager existing = FindObjectOfType<PlayerUpgradeManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("Player Upgrade Manager");
        return managerObject.AddComponent<PlayerUpgradeManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ClampLevels();
    }

    private void OnValidate()
    {
        maxLevel = Mathf.Max(1, maxLevel);
        moveSpeedBonusPerLevel = Mathf.Max(0f, moveSpeedBonusPerLevel);
        gatherSpeedBonusPerLevel = Mathf.Max(0f, gatherSpeedBonusPerLevel);
        ClampLevels();
    }

    public int GetLevel(PlayerUpgradeType type)
    {
        switch (type)
        {
            case PlayerUpgradeType.MoveSpeed:
                return moveSpeedLevel;
            case PlayerUpgradeType.WoodGatherSpeed:
                return woodGatherSpeedLevel;
            case PlayerUpgradeType.StoneGatherSpeed:
                return stoneGatherSpeedLevel;
            default:
                return 0;
        }
    }

    public int GetMaxLevel(PlayerUpgradeType type)
    {
        return maxLevel;
    }

    public bool IsMaxLevel(PlayerUpgradeType type)
    {
        return GetLevel(type) >= GetMaxLevel(type);
    }

    public int GetNextCost(PlayerUpgradeType type)
    {
        int level = GetLevel(type);
        if (level >= GetMaxLevel(type))
        {
            return 0;
        }

        int[] costs = GetCosts(type);
        if (costs == null || costs.Length == 0)
        {
            return 0;
        }

        return costs[Mathf.Clamp(level, 0, costs.Length - 1)];
    }

    public bool CanUpgrade(PlayerUpgradeType type)
    {
        if (IsMaxLevel(type) || ResourceManager.Instance == null)
        {
            return false;
        }

        return ResourceManager.Instance.CanSpend(ResourceType.Coin, GetNextCost(type));
    }

    public bool TryUpgrade(PlayerUpgradeType type)
    {
        if (IsMaxLevel(type) || ResourceManager.Instance == null)
        {
            return false;
        }

        int cost = GetNextCost(type);
        if (!ResourceManager.Instance.Spend(ResourceType.Coin, cost))
        {
            return false;
        }

        SetLevel(type, GetLevel(type) + 1);
        UpgradesChanged?.Invoke();
        return true;
    }

    public int GetCombinedGatherLevel()
    {
        return Mathf.Min(woodGatherSpeedLevel, stoneGatherSpeedLevel);
    }

    public int GetCombinedGatherMaxLevel()
    {
        return Mathf.Max(woodGatherSpeedLevel, stoneGatherSpeedLevel);
    }

    public int GetCombinedGatherCost()
    {
        int cost = 0;
        if (!IsMaxLevel(PlayerUpgradeType.WoodGatherSpeed))
        {
            cost += GetNextCost(PlayerUpgradeType.WoodGatherSpeed);
        }

        if (!IsMaxLevel(PlayerUpgradeType.StoneGatherSpeed))
        {
            cost += GetNextCost(PlayerUpgradeType.StoneGatherSpeed);
        }

        return cost;
    }

    public bool CanUpgradeCombinedGather()
    {
        if (ResourceManager.Instance == null)
        {
            return false;
        }

        if (IsMaxLevel(PlayerUpgradeType.WoodGatherSpeed) && IsMaxLevel(PlayerUpgradeType.StoneGatherSpeed))
        {
            return false;
        }

        return ResourceManager.Instance.CanSpend(ResourceType.Coin, GetCombinedGatherCost());
    }

    public bool TryUpgradeCombinedGather()
    {
        if (ResourceManager.Instance == null)
        {
            return false;
        }

        int totalCost = GetCombinedGatherCost();
        if (totalCost <= 0 || !ResourceManager.Instance.Spend(ResourceType.Coin, totalCost))
        {
            return false;
        }

        if (!IsMaxLevel(PlayerUpgradeType.WoodGatherSpeed))
        {
            woodGatherSpeedLevel = Mathf.Clamp(woodGatherSpeedLevel + 1, 0, maxLevel);
        }

        if (!IsMaxLevel(PlayerUpgradeType.StoneGatherSpeed))
        {
            stoneGatherSpeedLevel = Mathf.Clamp(stoneGatherSpeedLevel + 1, 0, maxLevel);
        }

        UpgradesChanged?.Invoke();
        return true;
    }

    public float GetGatherTickInterval(ResourceType resourceType, float baseInterval)
    {
        float speedMultiplier = 1f;
        if (resourceType == ResourceType.Wood)
        {
            speedMultiplier = WoodGatherSpeedMultiplier;
        }
        else if (resourceType == ResourceType.Stone)
        {
            speedMultiplier = StoneGatherSpeedMultiplier;
        }

        return Mathf.Max(0.05f, baseInterval / Mathf.Max(0.01f, speedMultiplier));
    }

    public string GetDisplayName(PlayerUpgradeType type)
    {
        switch (type)
        {
            case PlayerUpgradeType.MoveSpeed:
                return "移動速度";
            case PlayerUpgradeType.WoodGatherSpeed:
                return "木頭採集速度";
            case PlayerUpgradeType.StoneGatherSpeed:
                return "石頭採集速度";
            default:
                return "升級";
        }
    }

    public string GetEffectText(PlayerUpgradeType type)
    {
        int level = GetLevel(type);
        int nextLevel = Mathf.Min(level + 1, GetMaxLevel(type));
        int currentPercent = Mathf.RoundToInt((GetMultiplierAtLevel(type, level) - 1f) * 100f);
        int nextPercent = Mathf.RoundToInt((GetMultiplierAtLevel(type, nextLevel) - 1f) * 100f);

        if (IsMaxLevel(type))
        {
            return $"目前 +{currentPercent}%";
        }

        return $"目前 +{currentPercent}%  ->  下級 +{nextPercent}%";
    }

    private float GetMultiplierAtLevel(PlayerUpgradeType type, int level)
    {
        switch (type)
        {
            case PlayerUpgradeType.MoveSpeed:
                return 1f + level * moveSpeedBonusPerLevel;
            case PlayerUpgradeType.WoodGatherSpeed:
            case PlayerUpgradeType.StoneGatherSpeed:
                return 1f + level * gatherSpeedBonusPerLevel;
            default:
                return 1f;
        }
    }

    private int[] GetCosts(PlayerUpgradeType type)
    {
        switch (type)
        {
            case PlayerUpgradeType.MoveSpeed:
                return moveSpeedCosts;
            case PlayerUpgradeType.WoodGatherSpeed:
                return woodGatherSpeedCosts;
            case PlayerUpgradeType.StoneGatherSpeed:
                return stoneGatherSpeedCosts;
            default:
                return null;
        }
    }

    private void SetLevel(PlayerUpgradeType type, int level)
    {
        level = Mathf.Clamp(level, 0, GetMaxLevel(type));
        switch (type)
        {
            case PlayerUpgradeType.MoveSpeed:
                moveSpeedLevel = level;
                break;
            case PlayerUpgradeType.WoodGatherSpeed:
                woodGatherSpeedLevel = level;
                break;
            case PlayerUpgradeType.StoneGatherSpeed:
                stoneGatherSpeedLevel = level;
                break;
        }
    }

    private void ClampLevels()
    {
        moveSpeedLevel = Mathf.Clamp(moveSpeedLevel, 0, maxLevel);
        woodGatherSpeedLevel = Mathf.Clamp(woodGatherSpeedLevel, 0, maxLevel);
        stoneGatherSpeedLevel = Mathf.Clamp(stoneGatherSpeedLevel, 0, maxLevel);
    }
}
