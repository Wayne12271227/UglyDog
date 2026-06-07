using System.Collections.Generic;
using UnityEngine;

public class MushroomSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private GameObject mushroomPrefab;
    [SerializeField] private string floorRootName = "floor_grass";
    [SerializeField] private float spawnInterval = 10f;
    [SerializeField] private int maxActiveMushrooms = 3;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private float surfaceOffset = 0.08f;
    [SerializeField] private float groundProbeHeight = 5f;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Reward")]
    [SerializeField] private int minCoinReward = 30;
    [SerializeField] private int maxCoinReward = 50;
    [SerializeField] private float despawnAfterSeconds = 45f;

    private readonly List<MushroomPickup> activeMushrooms = new List<MushroomPickup>();
    private readonly List<Renderer> floorRenderers = new List<Renderer>();
    private Transform floorRoot;
    private float nextSpawnTime;

    private void Awake()
    {
        CacheFloorRenderers();
    }

    private void OnEnable()
    {
        nextSpawnTime = Time.time + (spawnOnStart ? 0f : Mathf.Max(0.1f, spawnInterval));
    }

    private void Update()
    {
        CleanupMissingMushrooms();

        if (Time.time < nextSpawnTime)
        {
            return;
        }

        nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnInterval);
        if (activeMushrooms.Count >= Mathf.Max(1, maxActiveMushrooms))
        {
            return;
        }

        SpawnMushroom();
    }

    private void SpawnMushroom()
    {
        if (mushroomPrefab == null)
        {
            Debug.LogWarning("MushroomSpawner has no mushroom prefab assigned.");
            return;
        }

        if (floorRenderers.Count == 0)
        {
            CacheFloorRenderers();
        }

        if (floorRenderers.Count == 0)
        {
            Debug.LogWarning("MushroomSpawner could not find floor renderers under " + floorRootName + ".");
            return;
        }

        Vector3 spawnPosition = GetRandomFloorPosition();
        Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject mushroomObject = Instantiate(mushroomPrefab, spawnPosition, spawnRotation);
        mushroomObject.name = "Coin Mushroom";

        MushroomPickup pickup = mushroomObject.GetComponent<MushroomPickup>();
        if (pickup == null)
        {
            pickup = mushroomObject.AddComponent<MushroomPickup>();
        }

        pickup.ConfigureReward(minCoinReward, maxCoinReward);
        pickup.ConfigureDespawn(despawnAfterSeconds);
        activeMushrooms.Add(pickup);
    }

    private Vector3 GetRandomFloorPosition()
    {
        Renderer renderer = floorRenderers[Random.Range(0, floorRenderers.Count)];
        Bounds bounds = renderer.bounds;
        float x = Random.Range(bounds.min.x, bounds.max.x);
        float z = Random.Range(bounds.min.z, bounds.max.z);
        Vector3 fallback = new Vector3(x, bounds.max.y + surfaceOffset, z);

        Vector3 probeStart = new Vector3(x, bounds.max.y + groundProbeHeight, z);
        float probeDistance = groundProbeHeight * 2f + 2f;
        if (Physics.Raycast(probeStart, Vector3.down, out RaycastHit hit, probeDistance, groundLayers, QueryTriggerInteraction.Ignore)
            && IsUnderFloorRoot(hit.transform))
        {
            return hit.point + Vector3.up * surfaceOffset;
        }

        return fallback;
    }

    private void CacheFloorRenderers()
    {
        floorRenderers.Clear();
        GameObject floorObject = GameObject.Find(floorRootName);
        floorRoot = floorObject != null ? floorObject.transform : null;
        if (floorRoot == null)
        {
            return;
        }

        Renderer[] renderers = floorRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || renderer.bounds.size.x <= 0.1f || renderer.bounds.size.z <= 0.1f)
            {
                continue;
            }

            floorRenderers.Add(renderer);
        }
    }

    private bool IsUnderFloorRoot(Transform target)
    {
        return floorRoot == null || target == floorRoot || target.IsChildOf(floorRoot);
    }

    private void CleanupMissingMushrooms()
    {
        for (int i = activeMushrooms.Count - 1; i >= 0; i--)
        {
            if (activeMushrooms[i] == null)
            {
                activeMushrooms.RemoveAt(i);
            }
        }
    }
}
