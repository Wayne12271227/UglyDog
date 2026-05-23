using UnityEngine;

[RequireComponent(typeof(BuildingHealth))]
public class AutoResourceBuilding : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType = ResourceType.Wood;
    [SerializeField] private int amountPerTick = 1;
    [SerializeField] private float tickInterval = 2f;

    private BuildingHealth health;
    private float nextTickTime;

    public ResourceType ResourceType => resourceType;
    public int AmountPerTick => amountPerTick;
    public float TickInterval => tickInterval;

    private void Awake()
    {
        health = GetComponent<BuildingHealth>();
        nextTickTime = Time.time + tickInterval;
    }

    private void OnValidate()
    {
        if (resourceType == ResourceType.Coin)
        {
            resourceType = ResourceType.Wood;
        }

        amountPerTick = Mathf.Max(1, amountPerTick);
        tickInterval = Mathf.Max(0.1f, tickInterval);
    }

    private void Update()
    {
        if (health != null && health.IsDestroyed)
        {
            return;
        }

        if (Time.time < nextTickTime)
        {
            return;
        }

        nextTickTime = Time.time + tickInterval;
        if (ResourceManager.Instance == null)
        {
            return;
        }

        ResourceManager.Instance.Add(resourceType, amountPerTick);
    }

    public void Configure(ResourceType type, int amount, float interval)
    {
        resourceType = type == ResourceType.Coin ? ResourceType.Wood : type;
        amountPerTick = Mathf.Max(1, amount);
        tickInterval = Mathf.Max(0.1f, interval);
        nextTickTime = Time.time + tickInterval;
    }
}
