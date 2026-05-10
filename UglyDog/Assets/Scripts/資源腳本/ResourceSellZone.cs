using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ResourceSellZone : MonoBehaviour
{
    [Header("Trade")]
    [SerializeField] private ResourceType resourceToSell = ResourceType.Wood;
    [SerializeField] private int resourcePerTick = 1;
    [SerializeField] private int coinsPerTick = 1;
    [SerializeField] private float tickInterval = 0.5f;
    [SerializeField] private bool sellImmediatelyOnEnter = true;

    [Header("Detection")]
    [SerializeField] private bool requirePlayerController = true;
    [SerializeField] private LayerMask detectionLayers = ~0;

    private Collider zoneCollider;
    private CatPlayerController activePlayer;
    private int playersInside;
    private float nextSellTime;

    private void Reset()
    {
        zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;
    }

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;
    }

    private void Update()
    {
        CatPlayerController player = activePlayer != null ? activePlayer : FindPlayerInsideZone();
        bool playerDetected = playersInside > 0 || player != null;
        if (!playerDetected)
        {
            return;
        }

        if (Time.time < nextSellTime)
        {
            return;
        }

        nextSellTime = Time.time + tickInterval;
        TrySell();
    }

    private void OnValidate()
    {
        if (resourceToSell == ResourceType.Coin)
        {
            resourceToSell = ResourceType.Wood;
        }

        resourcePerTick = Mathf.Max(1, resourcePerTick);
        coinsPerTick = Mathf.Max(1, coinsPerTick);
        tickInterval = Mathf.Max(0.05f, tickInterval);
    }

    private void OnTriggerEnter(Collider other)
    {
        CatPlayerController player = GetPlayer(other);
        if (!IsPlayer(other))
        {
            return;
        }

        playersInside++;
        if (activePlayer == null)
        {
            activePlayer = player;
        }

        nextSellTime = sellImmediatelyOnEnter ? Time.time : Time.time + tickInterval;
    }

    private void OnTriggerExit(Collider other)
    {
        CatPlayerController player = GetPlayer(other);
        if (!IsPlayer(other))
        {
            return;
        }

        playersInside = Mathf.Max(0, playersInside - 1);
        if (activePlayer != null && player == activePlayer)
        {
            activePlayer = null;
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (!requirePlayerController)
        {
            return true;
        }

        return GetPlayer(other) != null;
    }

    private CatPlayerController GetPlayer(Collider other)
    {
        return PreferredPlayerFinder.GetPreferredPlayer(other);
    }

    private CatPlayerController FindPlayerInsideZone()
    {
        if (zoneCollider == null)
        {
            zoneCollider = GetComponent<Collider>();
        }

        Bounds bounds = zoneCollider.bounds;
        Collider[] hits = Physics.OverlapBox(bounds.center, bounds.extents, transform.rotation, detectionLayers, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            if (hit == zoneCollider)
            {
                continue;
            }

            CatPlayerController player = GetPlayer(hit);
            if (player != null)
            {
                return player;
            }
        }

        return null;
    }

    private void TrySell()
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogWarning("ResourceSellZone cannot find ResourceManager.");
            return;
        }

        if (!ResourceManager.Instance.Spend(resourceToSell, resourcePerTick))
        {
            return;
        }

        ResourceManager.Instance.Add(ResourceType.Coin, coinsPerTick);
    }
}
