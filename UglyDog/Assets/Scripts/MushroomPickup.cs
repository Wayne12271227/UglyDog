using UnityEngine;

public class MushroomPickup : MonoBehaviour
{
    [Header("Reward")]
    [SerializeField] private int minCoinReward = 30;
    [SerializeField] private int maxCoinReward = 50;

    [Header("Motion")]
    [SerializeField] private float bobAmplitude = 0.18f;
    [SerializeField] private float bobFrequency = 1.5f;
    [SerializeField] private float rotationSpeed = 120f;

    [Header("Pickup")]
    [SerializeField] private float pickupRadius = 1.1f;
    [SerializeField] private float despawnAfterSeconds = 45f;

    private Vector3 baseLocalPosition;
    private float bobPhase;
    private float despawnTime;
    private bool collected;

    private void Awake()
    {
        EnsurePickupPhysics();
    }

    private void OnEnable()
    {
        baseLocalPosition = transform.localPosition;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
        despawnTime = Time.time + Mathf.Max(1f, despawnAfterSeconds);
        collected = false;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        Vector3 offset = Vector3.up * (Mathf.Sin(Time.time * bobFrequency + bobPhase) * bobAmplitude);
        transform.localPosition = baseLocalPosition + offset;

        if (Time.time >= despawnTime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryCollect(collision.collider);
    }

    public void ConfigureReward(int minReward, int maxReward)
    {
        minCoinReward = Mathf.Max(0, minReward);
        maxCoinReward = Mathf.Max(minCoinReward, maxReward);
    }

    public void ConfigureDespawn(float seconds)
    {
        despawnAfterSeconds = Mathf.Max(1f, seconds);
        despawnTime = Time.time + despawnAfterSeconds;
    }

    private void TryCollect(Collider other)
    {
        if (collected || other == null)
        {
            return;
        }

        CatPlayerController player = other.GetComponentInParent<CatPlayerController>();
        if (player == null || !player.HasLocalPlayerAuthority() || ResourceManager.Instance == null)
        {
            return;
        }

        collected = true;
        int reward = Random.Range(minCoinReward, maxCoinReward + 1);
        ResourceManager.Instance.Add(ResourceType.Coin, reward);
        Destroy(gameObject);
    }

    private void EnsurePickupPhysics()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].isTrigger = true;
        }

        SphereCollider trigger = GetComponent<SphereCollider>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<SphereCollider>();
        }

        float worldScale = GetLargestWorldScale();
        float localRadius = Mathf.Max(0.1f, pickupRadius) / worldScale;
        trigger.isTrigger = true;
        trigger.radius = localRadius;
        trigger.center = Vector3.up * localRadius * 0.5f;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;
    }

    private float GetLargestWorldScale()
    {
        Vector3 scale = transform.lossyScale;
        return Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
    }
}
