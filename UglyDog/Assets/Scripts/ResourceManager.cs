using System;
using UnityEngine;

public enum ResourceType
{
    Coin,
    Wood,
    Stone
}

[Serializable]
public struct ResourceCost
{
    public ResourceType type;
    public int amount;
}

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    [Header("Starting Resources")]
    [SerializeField] private int startingCoins = 100;
    [SerializeField] private int startingWood = 0;
    [SerializeField] private int startingStone = 0;

    [Header("Current Resources")]
    [SerializeField] private int coins;
    [SerializeField] private int wood;
    [SerializeField] private int stone;

    public int Coins => coins;
    public int Wood => wood;
    public int Stone => stone;

    public event Action<ResourceType, int> ResourceChanged;
    public event Action ResourcesChanged;

    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Initialize()
    {
        if (Instance != null && Instance != this && Instance.isActiveAndEnabled)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (!initialized)
        {
            ResetToStartingResources();
            initialized = true;
        }

        ResourcesChanged?.Invoke();
    }

    private void OnValidate()
    {
        startingCoins = Mathf.Max(0, startingCoins);
        startingWood = Mathf.Max(0, startingWood);
        startingStone = Mathf.Max(0, startingStone);

        if (!Application.isPlaying)
        {
            coins = startingCoins;
            wood = startingWood;
            stone = startingStone;
        }

        ResourceChanged?.Invoke(ResourceType.Coin, coins);
        ResourceChanged?.Invoke(ResourceType.Wood, wood);
        ResourceChanged?.Invoke(ResourceType.Stone, stone);
        ResourcesChanged?.Invoke();
    }

    public void ResetToStartingResources()
    {
        coins = Mathf.Max(0, startingCoins);
        wood = Mathf.Max(0, startingWood);
        stone = Mathf.Max(0, startingStone);
    }

    public int GetAmount(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Coin:
                return coins;
            case ResourceType.Wood:
                return wood;
            case ResourceType.Stone:
                return stone;
            default:
                return 0;
        }
    }

    public void Add(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetAmount(type, GetAmount(type) + amount);
        if (type == ResourceType.Coin)
        {
            CatPlayerController player = PreferredPlayerFinder.FindPreferredPlayer();
            if (player != null)
            {
                player.PlayCoinGainSound();
            }
        }
    }

    public bool CanSpend(ResourceType type, int amount)
    {
        return amount >= 0 && GetAmount(type) >= amount;
    }

    public bool Spend(ResourceType type, int amount)
    {
        if (!CanSpend(type, amount))
        {
            return false;
        }

        SetAmount(type, GetAmount(type) - amount);
        return true;
    }

    public bool CanAfford(ResourceCost[] costs)
    {
        if (costs == null)
        {
            return true;
        }

        foreach (ResourceCost cost in costs)
        {
            if (!CanSpend(cost.type, cost.amount))
            {
                return false;
            }
        }

        return true;
    }

    public bool Spend(ResourceCost[] costs)
    {
        if (!CanAfford(costs))
        {
            return false;
        }

        foreach (ResourceCost cost in costs)
        {
            Spend(cost.type, cost.amount);
        }

        return true;
    }

    public void SetAmount(ResourceType type, int amount)
    {
        amount = Mathf.Max(0, amount);

        switch (type)
        {
            case ResourceType.Coin:
                coins = amount;
                break;
            case ResourceType.Wood:
                wood = amount;
                break;
            case ResourceType.Stone:
                stone = amount;
                break;
        }

        ResourceChanged?.Invoke(type, amount);
        ResourcesChanged?.Invoke();
    }
}
