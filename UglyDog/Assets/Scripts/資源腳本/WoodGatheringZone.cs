using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WoodGatheringZone : MonoBehaviour
{
    [SerializeField] private int woodPerTick = 1;
    [SerializeField] private float tickInterval = 0.5f;
    [SerializeField] private bool requirePlayerController = true;
    [SerializeField] private bool requirePlayerStopped = true;
    [SerializeField] private float stoppedInputThreshold = 0.05f;
    [SerializeField] private LayerMask detectionLayers = ~0;

    private int playersInside;
    private float nextGatherTime;
    private Collider zoneCollider;
    private CatPlayerController activePlayer;
    private bool playerWasGathering;

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
        CatPlayerController detectedPlayer = activePlayer != null ? activePlayer : FindPlayerInsideZone();
        bool playerDetected = playersInside > 0 || detectedPlayer != null;
        if (!playerDetected)
        {
            playerWasGathering = false;
            return;
        }

        if (!CanGatherNow(detectedPlayer))
        {
            if (playerWasGathering && detectedPlayer != null)
            {
                detectedPlayer.StopAction();
            }

            playerWasGathering = false;
            return;
        }

        if (!playerWasGathering)
        {
            nextGatherTime = Time.time + GetEffectiveTickInterval();
        }

        if (detectedPlayer != null)
        {
            detectedPlayer.PlayDig();
        }

        playerWasGathering = true;

        if (Time.time < nextGatherTime)
        {
            return;
        }

        nextGatherTime = Time.time + GetEffectiveTickInterval();
        AddWood(detectedPlayer);
    }

    private void OnValidate()
    {
        woodPerTick = Mathf.Max(1, woodPerTick);
        tickInterval = Mathf.Max(0.05f, tickInterval);
        stoppedInputThreshold = Mathf.Max(0f, stoppedInputThreshold);
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

        nextGatherTime = Time.time;
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

        playerWasGathering = false;

        if (player != null)
        {
            player.StopAction();
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

    private bool CanGatherNow(CatPlayerController player)
    {
        return player != null
            && (!requirePlayerStopped || !player.HasMovementInput(stoppedInputThreshold));
    }

    private float GetEffectiveTickInterval()
    {
        PlayerUpgradeManager upgrades = PlayerUpgradeManager.Instance;
        return upgrades != null ? upgrades.GetGatherTickInterval(ResourceType.Wood, tickInterval) : tickInterval;
    }

    private void AddWood(CatPlayerController player)
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogWarning("WoodGatheringZone cannot find ResourceManager.");
            return;
        }

        ResourceManager.Instance.Add(ResourceType.Wood, woodPerTick);
    }
}
