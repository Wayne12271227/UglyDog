using UnityEngine;

[ExecuteAlways]
public class ToonCharacterSetup : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private string targetRootName;
    [SerializeField] private Transform targetRoot;

    [Header("Materials")]
    [SerializeField] private Material baseToonMaterial;
    [SerializeField] private string toonShaderName = "Custom/ToonLitOutline";
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private bool enableOutline = true;
    [SerializeField] private bool preserveExistingMaterialTextures = true;

    [Header("Toon Tuning")]
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField] private Color shadowColor = new Color(0.72f, 0.62f, 0.58f, 1f);
    [SerializeField, Range(0f, 1f)] private float shadowThreshold = 0.5f;
    [SerializeField, Range(0.001f, 0.25f)] private float shadowSmoothness = 0.05f;
    [SerializeField] private Color rimColor = new Color(1f, 0.95f, 0.9f, 1f);
    [SerializeField, Range(0.5f, 8f)] private float rimPower = 3f;
    [SerializeField, Range(0f, 1f)] private float rimStrength = 0.2f;
    [SerializeField] private Color outlineColor = new Color(0.14f, 0.08f, 0.06f, 1f);
    [SerializeField, Range(0f, 0.08f)] private float outlineWidth = 0.011f;

    private bool editorApplyQueued;

    private void OnEnable()
    {
        QueueOrApplyToonStyle();
    }

    private void OnValidate()
    {
        QueueOrApplyToonStyle();
    }

    [ContextMenu("Apply Toon Style")]
    public void ApplyToonStyle()
    {
        var toonShader = ResolveToonShader();
        if (baseToonMaterial == null && toonShader == null)
        {
            return;
        }

        var root = ResolveTargetRoot();
        if (root == null)
        {
            return;
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer.GetComponent<CharacterOutlineProxy>() != null)
            {
                continue;
            }

            ApplyBaseMaterial(renderer, toonShader);
            ApplyMaterialTuning(renderer);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            if (enableOutline && outlineMaterial != null)
            {
                EnsureOutlineProxy(renderer);
            }
        }
    }

    private void QueueOrApplyToonStyle()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (editorApplyQueued)
            {
                return;
            }

            editorApplyQueued = true;
            UnityEditor.EditorApplication.delayCall += ApplyToonStyleInEditor;
            return;
        }
#endif

        ApplyToonStyle();
    }

    private void ApplyToonStyleInEditor()
    {
#if UNITY_EDITOR
        editorApplyQueued = false;
        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        ApplyToonStyle();
#endif
    }

    private Transform ResolveTargetRoot()
    {
        if (targetRoot != null)
        {
            return targetRoot;
        }

        if (string.IsNullOrWhiteSpace(targetRootName))
        {
            return null;
        }

        var found = GameObject.Find(targetRootName);
        return found != null ? found.transform : null;
    }

    private Shader ResolveToonShader()
    {
        if (baseToonMaterial != null)
        {
            return baseToonMaterial.shader;
        }

        return string.IsNullOrWhiteSpace(toonShaderName) ? null : Shader.Find(toonShaderName);
    }

    private void ApplyBaseMaterial(Renderer renderer, Shader toonShader)
    {
        var materials = renderer.sharedMaterials;
        var changed = false;

        for (var i = 0; i < materials.Length; i++)
        {
            if (baseToonMaterial != null && !preserveExistingMaterialTextures)
            {
                if (materials[i] != baseToonMaterial)
                {
                    materials[i] = baseToonMaterial;
                    changed = true;
                }

                continue;
            }

            if (materials[i] != null && toonShader != null && materials[i].shader != toonShader)
            {
                materials[i].shader = toonShader;
                changed = true;
            }
        }

        if (changed)
        {
            renderer.sharedMaterials = materials;
        }
    }

    private void ApplyMaterialTuning(Renderer renderer)
    {
        var materials = renderer.sharedMaterials;
        foreach (var material in materials)
        {
            if (material == null)
            {
                continue;
            }

            SetColorIfAvailable(material, "_Color", baseColor);
            SetColorIfAvailable(material, "_ShadowColor", shadowColor);
            SetFloatIfAvailable(material, "_ShadowThreshold", shadowThreshold);
            SetFloatIfAvailable(material, "_ShadowSmoothness", shadowSmoothness);
            SetColorIfAvailable(material, "_RimColor", rimColor);
            SetFloatIfAvailable(material, "_RimPower", rimPower);
            SetFloatIfAvailable(material, "_RimStrength", rimStrength);
            SetColorIfAvailable(material, "_OutlineColor", outlineColor);
            SetFloatIfAvailable(material, "_OutlineWidth", outlineWidth);
        }

        if (outlineMaterial != null)
        {
            SetColorIfAvailable(outlineMaterial, "_OutlineColor", outlineColor);
            SetFloatIfAvailable(outlineMaterial, "_OutlineWidth", outlineWidth);
        }
    }

    private static void SetColorIfAvailable(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static void SetFloatIfAvailable(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private void EnsureOutlineProxy(Renderer sourceRenderer)
    {
        var outlineName = sourceRenderer.gameObject.name + "__Outline";
        var existing = sourceRenderer.transform.Find(outlineName);
        if (existing == null)
        {
            var outlineObject = new GameObject(outlineName);
            outlineObject.transform.SetParent(sourceRenderer.transform, false);
            outlineObject.AddComponent<CharacterOutlineProxy>();
            existing = outlineObject.transform;
        }

        existing.localPosition = Vector3.zero;
        existing.localRotation = Quaternion.identity;
        existing.localScale = Vector3.one;

        if (sourceRenderer is SkinnedMeshRenderer sourceSkinned)
        {
            var outlineSkinned = existing.GetComponent<SkinnedMeshRenderer>();
            if (outlineSkinned == null)
            {
                outlineSkinned = existing.gameObject.AddComponent<SkinnedMeshRenderer>();
            }

            outlineSkinned.sharedMesh = sourceSkinned.sharedMesh;
            outlineSkinned.rootBone = sourceSkinned.rootBone;
            outlineSkinned.bones = sourceSkinned.bones;
            outlineSkinned.updateWhenOffscreen = true;
            outlineSkinned.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineSkinned.receiveShadows = false;
            outlineSkinned.sharedMaterials = CreateOutlineMaterialArray(sourceSkinned.sharedMaterials.Length);
            return;
        }

        var sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
        if (sourceFilter == null)
        {
            return;
        }

        var outlineFilter = existing.GetComponent<MeshFilter>();
        if (outlineFilter == null)
        {
            outlineFilter = existing.gameObject.AddComponent<MeshFilter>();
        }

        outlineFilter.sharedMesh = sourceFilter.sharedMesh;

        var outlineMeshRenderer = existing.GetComponent<MeshRenderer>();
        if (outlineMeshRenderer == null)
        {
            outlineMeshRenderer = existing.gameObject.AddComponent<MeshRenderer>();
        }

        outlineMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineMeshRenderer.receiveShadows = false;
        outlineMeshRenderer.sharedMaterials = CreateOutlineMaterialArray(sourceRenderer.sharedMaterials.Length);
    }

    private Material[] CreateOutlineMaterialArray(int length)
    {
        var materials = new Material[length];
        for (var i = 0; i < length; i++)
        {
            materials[i] = outlineMaterial;
        }

        return materials;
    }
}
