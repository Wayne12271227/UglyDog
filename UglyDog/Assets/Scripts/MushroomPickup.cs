using UnityEngine;

public class MushroomPickup : MonoBehaviour
{
    private const string DiscoveryEffectRootName = "Gold Discovery FX";
    private const string SparkleEffectName = "Gold Sparkles";
    private const string RingEffectName = "Gold Ring";
    private const string LightEffectName = "Gold Glow";
    private static Material sharedGoldParticleMaterial;

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

    [Header("Discovery Visual")]
    [SerializeField] private bool showDiscoveryEffect = true;
    [SerializeField] private Color discoveryGold = new Color(1f, 0.92f, 0.12f, 0.9f);
    [SerializeField] private float discoveryRadius = 0.48f;
    [SerializeField] private float discoveryHeight = 0.16f;
    [SerializeField] private float sparkleRate = 20f;
    [SerializeField] private float ringRate = 12f;

    private Vector3 baseLocalPosition;
    private float bobPhase;
    private float despawnTime;
    private bool collected;
    private ParticleSystem sparkleEffect;
    private ParticleSystem ringEffect;
    private Light discoveryLight;

    private void Awake()
    {
        EnsurePickupPhysics();
        EnsureDiscoveryEffect();
    }

    private void OnEnable()
    {
        baseLocalPosition = transform.localPosition;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
        despawnTime = Time.time + Mathf.Max(1f, despawnAfterSeconds);
        collected = false;
        RestartDiscoveryEffect();
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

    private void EnsureDiscoveryEffect()
    {
        if (!showDiscoveryEffect)
        {
            return;
        }

        Transform effectRoot = transform.Find(DiscoveryEffectRootName);
        if (effectRoot == null)
        {
            GameObject effectObject = new GameObject(DiscoveryEffectRootName);
            effectRoot = effectObject.transform;
            effectRoot.SetParent(transform, false);
        }

        effectRoot.localPosition = Vector3.up * Mathf.Max(0.05f, discoveryHeight);
        effectRoot.localRotation = Quaternion.identity;
        effectRoot.localScale = Vector3.one;

        sparkleEffect = EnsureParticleSystem(effectRoot, SparkleEffectName);
        ringEffect = EnsureParticleSystem(effectRoot, RingEffectName);
        discoveryLight = EnsureGoldLight(effectRoot);

        ConfigureSparkles(sparkleEffect);
        ConfigureRing(ringEffect);
    }

    private ParticleSystem EnsureParticleSystem(Transform parent, string effectName)
    {
        Transform existing = parent.Find(effectName);
        if (existing != null)
        {
            ParticleSystem particles = existing.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                return particles;
            }
        }

        GameObject effectObject = new GameObject(effectName);
        effectObject.transform.SetParent(parent, false);
        effectObject.transform.localPosition = Vector3.zero;
        return effectObject.AddComponent<ParticleSystem>();
    }

    private Light EnsureGoldLight(Transform parent)
    {
        Transform existing = parent.Find(LightEffectName);
        Light light = existing != null ? existing.GetComponent<Light>() : null;
        if (light != null)
        {
            return light;
        }

        GameObject lightObject = new GameObject(LightEffectName);
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = Vector3.up * 0.25f;
        return lightObject.AddComponent<Light>();
    }

    private void ConfigureSparkles(ParticleSystem particles)
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.26f, 0.75f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
        main.startColor = discoveryGold;
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Max(0f, sparkleRate);

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.1f, discoveryRadius);
        shape.radiusThickness = 0.28f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = BuildFadeGradient(discoveryGold);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.07f, 0.24f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = GetGoldParticleMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 12;
        }
    }

    private void ConfigureRing(ParticleSystem particles)
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.11f);
        main.startColor = discoveryGold;
        main.maxParticles = 48;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Max(0f, ringRate);

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Donut;
        shape.radius = Mathf.Max(0.18f, discoveryRadius);
        shape.donutRadius = 0.05f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = BuildFadeGradient(discoveryGold);

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = GetGoldParticleMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 11;
        }
    }

    private Material GetGoldParticleMaterial()
    {
        if (sharedGoldParticleMaterial != null)
        {
            return sharedGoldParticleMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return null;
        }

        sharedGoldParticleMaterial = new Material(shader)
        {
            name = "Runtime Gold Mushroom Particle"
        };
        sharedGoldParticleMaterial.color = discoveryGold;
        return sharedGoldParticleMaterial;
    }

    private Gradient BuildFadeGradient(Color color)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(new Color(1f, 1f, 0.45f, 1f), 0.5f),
                new GradientColorKey(color, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(color.a, 0.15f),
                new GradientAlphaKey(color.a * 0.65f, 0.65f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    private void RestartDiscoveryEffect()
    {
        if (!showDiscoveryEffect)
        {
            return;
        }

        if (sparkleEffect != null)
        {
            sparkleEffect.Clear();
            sparkleEffect.Play();
        }

        if (ringEffect != null)
        {
            ringEffect.Clear();
            ringEffect.Play();
        }

        if (discoveryLight != null)
        {
            discoveryLight.type = LightType.Point;
            discoveryLight.color = discoveryGold;
            discoveryLight.range = Mathf.Max(1.4f, discoveryRadius * 3.2f);
            discoveryLight.intensity = 1.35f;
            discoveryLight.shadows = LightShadows.None;
        }
    }

    private float GetLargestWorldScale()
    {
        Vector3 scale = transform.lossyScale;
        return Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
    }
}
