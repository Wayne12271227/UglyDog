using System;
using UnityEngine;

public enum PlayerUpgradeType
{
    MoveSpeed,
    WoodGatherSpeed,
    StoneGatherSpeed,
    MeleeTraining,
    RangedTraining
}

public class PlayerUpgradeManager : MonoBehaviour
{
    public static PlayerUpgradeManager Instance { get; private set; }

    [Header("Levels")]
    [SerializeField] private int moveSpeedLevel;
    [SerializeField] private int woodGatherSpeedLevel;
    [SerializeField] private int stoneGatherSpeedLevel;
    [SerializeField] private int meleeTrainingLevel;
    [SerializeField] private int rangedTrainingLevel;
    [SerializeField] private int dogMeleeTrainingLevel;
    [SerializeField] private int dogRangedTrainingLevel;
    [SerializeField] private int catMeleeTrainingLevel;
    [SerializeField] private int catRangedTrainingLevel;

    [Header("Upgrade Limits")]
    [SerializeField] private int maxLevel = 5;
    [SerializeField] private int maxMinionTrainingLevel = 3;

    [Header("Bonus Per Level")]
    [SerializeField] private float moveSpeedBonusPerLevel = 0.1f;
    [SerializeField] private float gatherSpeedBonusPerLevel = 0.15f;
    [SerializeField] private float meleeHealthBonusPerLevel = 0.2f;
    [SerializeField] private float meleeBuildingDamageBonusPerLevel = 0.1f;
    [SerializeField] private float rangedDamageBonusPerLevel = 0.2f;
    [SerializeField] private float rangedRangeBonusPerLevel = 0.1f;
    [SerializeField] private int meleeTrainingHealthBonusPerLevel = 18;
    [SerializeField] private int meleeTrainingDamageBonusPerLevel = 1;
    [SerializeField] private int rangedTrainingHealthBonusPerLevel = 3;
    [SerializeField] private int rangedTrainingDamageBonusPerLevel = 6;

    [Header("Costs")]
    [SerializeField] private int maxUpgradeCost = 200;
    [SerializeField] private int[] moveSpeedCosts = { 50, 85, 120, 155, 200 };
    [SerializeField] private int[] gatherSpeedCosts = { 50, 85, 120, 155, 200 };
    [SerializeField] private int[] woodGatherSpeedCosts = { 50, 85, 120, 155, 200 };
    [SerializeField] private int[] stoneGatherSpeedCosts = { 50, 85, 120, 155, 200 };
    [SerializeField] private int[] meleeTrainingCosts = { 100, 150, 200 };
    [SerializeField] private int[] rangedTrainingCosts = { 100, 150, 200 };

    public event Action UpgradesChanged;

    public float MoveSpeedMultiplier => 1f + moveSpeedLevel * moveSpeedBonusPerLevel;
    public float WoodGatherSpeedMultiplier => 1f + woodGatherSpeedLevel * gatherSpeedBonusPerLevel;
    public float StoneGatherSpeedMultiplier => 1f + stoneGatherSpeedLevel * gatherSpeedBonusPerLevel;
    public float GatherSpeedMultiplier => 1f + GetGatherSpeedLevel() * gatherSpeedBonusPerLevel;
    public float MeleeHealthMultiplier => 1f;
    public float MeleeBuildingDamageMultiplier => 1f;
    public float RangedDamageMultiplier => 1f;
    public float RangedRangeMultiplier => 1f;

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
        maxMinionTrainingLevel = Mathf.Max(1, maxMinionTrainingLevel);
        maxUpgradeCost = Mathf.Max(1, maxUpgradeCost);
        moveSpeedBonusPerLevel = Mathf.Max(0f, moveSpeedBonusPerLevel);
        gatherSpeedBonusPerLevel = Mathf.Max(0f, gatherSpeedBonusPerLevel);
        meleeHealthBonusPerLevel = Mathf.Max(0f, meleeHealthBonusPerLevel);
        meleeBuildingDamageBonusPerLevel = Mathf.Max(0f, meleeBuildingDamageBonusPerLevel);
        rangedDamageBonusPerLevel = Mathf.Max(0f, rangedDamageBonusPerLevel);
        rangedRangeBonusPerLevel = Mathf.Max(0f, rangedRangeBonusPerLevel);
        meleeTrainingHealthBonusPerLevel = Mathf.Max(0, meleeTrainingHealthBonusPerLevel);
        meleeTrainingDamageBonusPerLevel = Mathf.Max(0, meleeTrainingDamageBonusPerLevel);
        rangedTrainingHealthBonusPerLevel = Mathf.Max(0, rangedTrainingHealthBonusPerLevel);
        rangedTrainingDamageBonusPerLevel = Mathf.Max(0, rangedTrainingDamageBonusPerLevel);
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
            case PlayerUpgradeType.MeleeTraining:
                return meleeTrainingLevel;
            case PlayerUpgradeType.RangedTraining:
                return rangedTrainingLevel;
            default:
                return 0;
        }
    }

    public int GetMaxLevel(PlayerUpgradeType type)
    {
        if (type == PlayerUpgradeType.MeleeTraining || type == PlayerUpgradeType.RangedTraining)
        {
            return maxMinionTrainingLevel;
        }

        return maxLevel;
    }

    public int GetLevel(PlayerUpgradeType type, MinionTeam team)
    {
        if (type == PlayerUpgradeType.MeleeTraining)
        {
            return team == MinionTeam.Cat ? catMeleeTrainingLevel : dogMeleeTrainingLevel;
        }

        if (type == PlayerUpgradeType.RangedTraining)
        {
            return team == MinionTeam.Cat ? catRangedTrainingLevel : dogRangedTrainingLevel;
        }

        return GetLevel(type);
    }

    public bool IsMaxLevel(PlayerUpgradeType type, MinionTeam team)
    {
        return GetLevel(type, team) >= GetMaxLevel(type);
    }

    public int GetNextCost(PlayerUpgradeType type, MinionTeam team)
    {
        int level = GetLevel(type, team);
        if (level >= GetMaxLevel(type))
        {
            return 0;
        }

        int[] costs = GetCosts(type);
        if (costs == null || costs.Length == 0)
        {
            return 0;
        }

        return ApplyUpgradeCostCap(costs[Mathf.Clamp(level, 0, costs.Length - 1)]);
    }

    public bool TryUpgrade(PlayerUpgradeType type, MinionTeam team)
    {
        if ((type != PlayerUpgradeType.MeleeTraining && type != PlayerUpgradeType.RangedTraining)
            || IsMaxLevel(type, team)
            || ResourceManager.Instance == null)
        {
            return false;
        }

        int cost = GetNextCost(type, team);
        if (!ResourceManager.Instance.Spend(ResourceType.Coin, cost))
        {
            return false;
        }

        SetLevel(type, team, GetLevel(type, team) + 1);
        UpgradesChanged?.Invoke();
        return true;
    }

    public int GetMeleeTrainingHealthBonus(MinionTeam team)
    {
        return GetLevel(PlayerUpgradeType.MeleeTraining, team) * meleeTrainingHealthBonusPerLevel;
    }

    public int GetMeleeTrainingDamageBonus(MinionTeam team)
    {
        return GetLevel(PlayerUpgradeType.MeleeTraining, team) * meleeTrainingDamageBonusPerLevel;
    }

    public int GetRangedTrainingHealthBonus(MinionTeam team)
    {
        return GetLevel(PlayerUpgradeType.RangedTraining, team) * rangedTrainingHealthBonusPerLevel;
    }

    public int GetRangedTrainingDamageBonus(MinionTeam team)
    {
        return GetLevel(PlayerUpgradeType.RangedTraining, team) * rangedTrainingDamageBonusPerLevel;
    }

    public int GetGatherSpeedLevel()
    {
        return Mathf.Min(woodGatherSpeedLevel, stoneGatherSpeedLevel);
    }

    public int GetGatherSpeedMaxLevel()
    {
        return maxLevel;
    }

    public bool IsMaxLevel(PlayerUpgradeType type)
    {
        return GetLevel(type) >= GetMaxLevel(type);
    }

    public bool IsGatherSpeedMaxLevel()
    {
        return woodGatherSpeedLevel >= maxLevel && stoneGatherSpeedLevel >= maxLevel;
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

        return ApplyUpgradeCostCap(costs[Mathf.Clamp(level, 0, costs.Length - 1)]);
    }

    public int GetGatherSpeedNextCost()
    {
        if (IsGatherSpeedMaxLevel())
        {
            return 0;
        }

        if (gatherSpeedCosts == null || gatherSpeedCosts.Length == 0)
        {
            return 0;
        }

        return ApplyUpgradeCostCap(gatherSpeedCosts[Mathf.Clamp(GetGatherSpeedLevel(), 0, gatherSpeedCosts.Length - 1)]);
    }

    public bool CanUpgrade(PlayerUpgradeType type)
    {
        if (IsMaxLevel(type) || ResourceManager.Instance == null)
        {
            return false;
        }

        return ResourceManager.Instance.CanSpend(ResourceType.Coin, GetNextCost(type));
    }

    public bool CanUpgradeGatherSpeed()
    {
        if (IsGatherSpeedMaxLevel() || ResourceManager.Instance == null)
        {
            return false;
        }

        return ResourceManager.Instance.CanSpend(ResourceType.Coin, GetGatherSpeedNextCost());
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

    public bool TryUpgradeGatherSpeed()
    {
        if (IsGatherSpeedMaxLevel() || ResourceManager.Instance == null)
        {
            return false;
        }

        int cost = GetGatherSpeedNextCost();
        if (!ResourceManager.Instance.Spend(ResourceType.Coin, cost))
        {
            return false;
        }

        if (woodGatherSpeedLevel < maxLevel)
        {
            woodGatherSpeedLevel++;
        }

        if (stoneGatherSpeedLevel < maxLevel)
        {
            stoneGatherSpeedLevel++;
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
            case PlayerUpgradeType.MeleeTraining:
                return 1f + level * meleeHealthBonusPerLevel;
            case PlayerUpgradeType.RangedTraining:
                return 1f + level * rangedDamageBonusPerLevel;
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
            case PlayerUpgradeType.MeleeTraining:
                return meleeTrainingCosts;
            case PlayerUpgradeType.RangedTraining:
                return rangedTrainingCosts;
            default:
                return null;
        }
    }

    private int GetCostAtLevel(PlayerUpgradeType type, int level)
    {
        int[] costs = GetCosts(type);
        if (costs == null || costs.Length == 0)
        {
            return 0;
        }

        return ApplyUpgradeCostCap(costs[Mathf.Clamp(level, 0, costs.Length - 1)]);
    }

    private int ApplyUpgradeCostCap(int cost)
    {
        return cost <= 0 ? 0 : Mathf.Min(cost, maxUpgradeCost);
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
            case PlayerUpgradeType.MeleeTraining:
                meleeTrainingLevel = level;
                break;
            case PlayerUpgradeType.RangedTraining:
                rangedTrainingLevel = level;
                break;
        }
    }

    private void SetLevel(PlayerUpgradeType type, MinionTeam team, int level)
    {
        level = Mathf.Clamp(level, 0, GetMaxLevel(type));
        if (type == PlayerUpgradeType.MeleeTraining)
        {
            if (team == MinionTeam.Cat)
            {
                catMeleeTrainingLevel = level;
            }
            else
            {
                dogMeleeTrainingLevel = level;
            }
        }
        else if (type == PlayerUpgradeType.RangedTraining)
        {
            if (team == MinionTeam.Cat)
            {
                catRangedTrainingLevel = level;
            }
            else
            {
                dogRangedTrainingLevel = level;
            }
        }
    }

    private void ClampLevels()
    {
        moveSpeedLevel = Mathf.Clamp(moveSpeedLevel, 0, maxLevel);
        woodGatherSpeedLevel = Mathf.Clamp(woodGatherSpeedLevel, 0, maxLevel);
        stoneGatherSpeedLevel = Mathf.Clamp(stoneGatherSpeedLevel, 0, maxLevel);
        meleeTrainingLevel = Mathf.Clamp(meleeTrainingLevel, 0, maxMinionTrainingLevel);
        rangedTrainingLevel = Mathf.Clamp(rangedTrainingLevel, 0, maxMinionTrainingLevel);
        dogMeleeTrainingLevel = Mathf.Clamp(dogMeleeTrainingLevel, 0, maxMinionTrainingLevel);
        dogRangedTrainingLevel = Mathf.Clamp(dogRangedTrainingLevel, 0, maxMinionTrainingLevel);
        catMeleeTrainingLevel = Mathf.Clamp(catMeleeTrainingLevel, 0, maxMinionTrainingLevel);
        catRangedTrainingLevel = Mathf.Clamp(catRangedTrainingLevel, 0, maxMinionTrainingLevel);
    }
}
