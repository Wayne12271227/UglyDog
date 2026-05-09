using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class InteractiveAreaVisual : MonoBehaviour
{
    [Header("Line")]
    [SerializeField] private Color lineColor = new Color(0.15f, 0.85f, 1f, 1f);
    [SerializeField] private float glowIntensity = 2.5f;

    [Header("Particles")]
    [SerializeField] private bool enableParticles;
    [SerializeField] private Color particleColor = new Color(0.15f, 0.85f, 1f, 0.75f);
    [SerializeField] private float particleRadius = 2.1f;
    [SerializeField] private float particlesPerSecond = 14f;
    [SerializeField] private float particleLifetime = 1.25f;
    [SerializeField] private float particleSize = 0.08f;

    private const string ParticleObjectName = "Interaction Particles";

    private Renderer cachedRenderer;
    private MaterialPropertyBlock propertyBlock;
    private ParticleSystem cachedParticles;

    public Color LineColor
    {
        get => lineColor;
        set
        {
            lineColor = value;
            particleColor = new Color(value.r, value.g, value.b, particleColor.a);
            ApplyVisuals();
        }
    }

    private void OnEnable()
    {
        RefreshVisuals();
    }

    [ContextMenu("Refresh Interactive Area Visual")]
    public void RefreshVisuals()
    {
        CacheComponents();
        EnsureParticles();
        ApplyVisuals();
    }

    private void OnValidate()
    {
        glowIntensity = Mathf.Max(0f, glowIntensity);
        particleRadius = Mathf.Max(0.1f, particleRadius);
        particlesPerSecond = Mathf.Max(0f, particlesPerSecond);
        particleLifetime = Mathf.Max(0.1f, particleLifetime);
        particleSize = Mathf.Max(0.01f, particleSize);

        CacheComponents();
        ApplyVisuals();
    }

    private void CacheComponents()
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        if (cachedParticles == null)
        {
            Transform particleTransform = transform.Find(ParticleObjectName);
            if (particleTransform != null)
            {
                cachedParticles = particleTransform.GetComponent<ParticleSystem>();
            }
        }
    }

    private void EnsureParticles()
    {
        if (!enableParticles || cachedParticles != null)
        {
            return;
        }

        GameObject particleObject = new GameObject(ParticleObjectName, typeof(ParticleSystem));
        particleObject.transform.SetParent(transform, false);
        particleObject.transform.localPosition = Vector3.zero;
        cachedParticles = particleObject.GetComponent<ParticleSystem>();
    }

    private void ApplyVisuals()
    {
        ApplyLineVisuals();
        ApplyParticleVisuals();
    }

    private void ApplyLineVisuals()
    {
        if (cachedRenderer == null)
        {
            return;
        }

        cachedRenderer.GetPropertyBlock(propertyBlock);

        Color litColor = lineColor * Mathf.Max(1f, glowIntensity);
        litColor.a = lineColor.a;
        propertyBlock.SetColor("_Color", litColor);
        propertyBlock.SetColor("_BaseColor", litColor);
        propertyBlock.SetColor("_EmissionColor", lineColor * glowIntensity);

        cachedRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyParticleVisuals()
    {
        if (cachedParticles == null)
        {
            return;
        }

        GameObject particleObject = cachedParticles.gameObject;
        if (particleObject.activeSelf != enableParticles)
        {
            particleObject.SetActive(enableParticles);
        }

        if (!enableParticles)
        {
            return;
        }

        ParticleSystem.MainModule main = cachedParticles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = particleLifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.6f, particleSize * 1.4f);
        main.startColor = particleColor;
        main.maxParticles = Mathf.CeilToInt(Mathf.Max(16f, particlesPerSecond * particleLifetime * 3f));

        ParticleSystem.EmissionModule emission = cachedParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = particlesPerSecond;

        ParticleSystem.ShapeModule shape = cachedParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Donut;
        shape.radius = particleRadius;
        shape.donutRadius = 0.06f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = cachedParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(particleColor, 0f),
                new GradientColorKey(lineColor, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(particleColor.a, 0.18f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.VelocityOverLifetimeModule velocity = cachedParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);

        ParticleSystemRenderer particleRenderer = cachedParticles.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortingOrder = 10;
        }

        if (Application.isPlaying && !cachedParticles.isPlaying)
        {
            cachedParticles.Play();
        }
    }
}
