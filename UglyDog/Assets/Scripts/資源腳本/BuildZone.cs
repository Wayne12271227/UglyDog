using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BuildZone : MonoBehaviour
{
    [Header("Cost")]
    [SerializeField] private ResourceCost[] costs =
    {
        new ResourceCost { type = ResourceType.Wood, amount = 10 },
        new ResourceCost { type = ResourceType.Stone, amount = 5 }
    };

    [Header("Build")]
    [SerializeField] private float buildDuration = 3f;
    [SerializeField] private bool buildOnce = true;
    [SerializeField] private bool keepProgressWhenPlayerLeaves = true;
    [SerializeField] private GameObject completedVisual;

    [Header("Detection")]
    [SerializeField] private bool requirePlayerController = true;
    [SerializeField] private LayerMask detectionLayers = ~0;

    private Collider zoneCollider;
    private CatPlayerController activeBuilder;
    private float buildProgress;
    private bool isBuilding;
    private bool isBuilt;
    private bool costPaid;
    private int playersInside;

    private void Reset()
    {
        zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;
    }

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;

        if (completedVisual != null)
        {
            completedVisual.SetActive(isBuilt);
        }
    }

    private void Update()
    {
        if (buildOnce && isBuilt)
        {
            return;
        }

        CatPlayerController builder = activeBuilder != null ? activeBuilder : FindPlayerInsideZone();
        if (builder == null)
        {
            PauseBuild(activeBuilder);
            activeBuilder = null;
            return;
        }

        activeBuilder = builder;
        if (!isBuilding && !TryStartBuild(builder))
        {
            return;
        }

        builder.PlayBuild();
        buildProgress += Time.deltaTime;

        if (buildProgress >= buildDuration)
        {
            CompleteBuild();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CatPlayerController player = GetPlayer(other);
        if (!IsPlayer(other))
        {
            return;
        }

        playersInside++;
        if (activeBuilder == null)
        {
            activeBuilder = player;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CatPlayerController player = GetPlayer(other);
        if (!IsPlayer(other))
        {
            return;
        }

        playersInside = Mathf.Max(0, playersInside - 1);
        if (player == activeBuilder)
        {
            activeBuilder = null;
        }

        if (playersInside == 0)
        {
            PauseBuild(player);
        }
    }

    private bool TryStartBuild(CatPlayerController builder)
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogWarning("BuildZone cannot find ResourceManager.");
            return false;
        }

        if (!costPaid)
        {
            if (!ResourceManager.Instance.CanAfford(costs))
            {
                return false;
            }

            ResourceManager.Instance.Spend(costs);
            costPaid = true;
        }

        isBuilding = true;
        activeBuilder = builder;
        builder.PlayBuild();
        return true;
    }

    private void PauseBuild(CatPlayerController builder)
    {
        isBuilding = false;
        if (!keepProgressWhenPlayerLeaves)
        {
            buildProgress = 0f;
        }

        if (builder != null)
        {
            builder.StopAction();
        }
    }

    private void CompleteBuild()
    {
        isBuilding = false;
        isBuilt = true;
        buildProgress = buildDuration;
        if (activeBuilder != null)
        {
            activeBuilder.StopAction();
            activeBuilder = null;
        }

        if (completedVisual != null)
        {
            completedVisual.SetActive(true);
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
        return other.GetComponentInParent<CatPlayerController>();
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
}
