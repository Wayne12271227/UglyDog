using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class TreesChoppingSystem : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private CatPlayerController player;
    [SerializeField] private bool autoFindPlayer = true;

    [Header("Chopping")]
    [SerializeField] private int woodPerTick = 1;
    [SerializeField] private float tickInterval = 1f;
    [SerializeField] private bool playDigAnimation = true;

    [Header("Range Visual Only")]
    [SerializeField] private float chopRange = 2.2f;
    [SerializeField] private float rangePadding = 0.65f;
    [SerializeField] private Vector3 rangeCenterOffset;
    [SerializeField] private Color rangeColor = new Color(0.15f, 1f, 0.22f, 0.95f);
    [SerializeField] private int dashCount = 24;
    [SerializeField] private float dashFill = 0.55f;
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private float ringHeight = 0.16f;

    private const string VisualRootName = "Tree Chop Range Visuals";

    private readonly List<TreeRange> treeRanges = new List<TreeRange>();
    private GameObject visualRoot;
    private Mesh visualMesh;
    private Material visualMaterial;
    private bool rebuilding;
    private bool playerWasInRange;
    private float nextGatherTime;
    private float nextPlayerSearchTime;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 lastScale;
#if UNITY_EDITOR
    private bool editorRebuildQueued;
#endif

    private struct TreeRange
    {
        public Vector3 center;
        public float radius;
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            RebuildIfTransformChanged();
            return;
        }

        if (treeRanges.Count == 0)
        {
            RebuildVisuals();
        }

        FindPlayerIfNeeded();
        bool playerInRange = player != null && IsPlayerInsideAnyRange(player.transform.position);
        if (playerInRange)
        {
            if (!playerWasInRange)
            {
                nextGatherTime = Time.time;
            }

            if (playDigAnimation)
            {
                player.PlayDig();
            }

            if (Time.time >= nextGatherTime)
            {
                nextGatherTime = Time.time + tickInterval;
                AddWood();
            }
        }
        else if (playerWasInRange && player != null && playDigAnimation)
        {
            player.StopAction();
        }

        playerWasInRange = playerInRange;
    }

    private void OnEnable()
    {
        QueueRebuild();
    }

    private void OnDisable()
    {
        rebuilding = true;
        try
        {
            CleanupVisuals();
        }
        finally
        {
            rebuilding = false;
        }
    }

    private void OnTransformChildrenChanged()
    {
        if (!rebuilding && isActiveAndEnabled)
        {
            QueueRebuild();
        }
    }

    private void OnValidate()
    {
        chopRange = Mathf.Max(0.25f, chopRange);
        rangePadding = Mathf.Max(0f, rangePadding);
        woodPerTick = Mathf.Max(1, woodPerTick);
        tickInterval = Mathf.Max(0.05f, tickInterval);
        dashCount = Mathf.Clamp(dashCount, 8, 64);
        dashFill = Mathf.Clamp(dashFill, 0.1f, 0.9f);
        lineWidth = Mathf.Max(0.01f, lineWidth);
        ringHeight = Mathf.Max(0.01f, ringHeight);

        QueueRebuild();
    }

    [ContextMenu("Rebuild Tree Range Visuals")]
    public void RebuildVisuals()
    {
        rebuilding = true;
        try
        {
            CleanupVisuals();
            CreateVisualRoot();
            CacheTransformState();

            treeRanges.Clear();
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            if (LooksLikeTree(transform))
            {
                AddRangeForTree(transform, vertices, triangles);
            }
            else
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform tree = transform.GetChild(i);
                    if (tree == null || tree.name == VisualRootName || !tree.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    AddRangeForTree(tree, vertices, triangles);
                }
            }

            visualMesh = new Mesh
            {
                name = "Tree Chop Range Mesh",
            };
            visualMesh.SetVertices(vertices);
            visualMesh.SetTriangles(triangles, 0);
            visualMesh.RecalculateBounds();
            visualMesh.RecalculateNormals();
            visualMesh.hideFlags = HideFlags.HideAndDontSave;

            MeshFilter filter = visualRoot.AddComponent<MeshFilter>();
            filter.sharedMesh = visualMesh;

            MeshRenderer renderer = visualRoot.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetVisualMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        finally
        {
            rebuilding = false;
        }
    }

    private void AddRangeForTree(Transform tree, List<Vector3> vertices, List<int> triangles)
    {
        Bounds bounds;
        if (!TryGetTreeBounds(tree, out bounds))
        {
            return;
        }

        float worldRadius = Mathf.Max(chopRange, Mathf.Max(bounds.extents.x, bounds.extents.z) + rangePadding);
        Vector3 anchor = tree.TransformPoint(rangeCenterOffset);
        Vector3 worldCenter = new Vector3(anchor.x, anchor.y + ringHeight, anchor.z);
        Vector3 localCenter = visualRoot.transform.InverseTransformPoint(worldCenter);
        float localScale = Mathf.Max(0.001f, GetHorizontalScale(visualRoot.transform));
        float localRadius = worldRadius / localScale;
        float localLineWidth = lineWidth / localScale;

        treeRanges.Add(new TreeRange { center = worldCenter, radius = worldRadius });
        AddDashedRing(vertices, triangles, localCenter, localRadius, localLineWidth);
    }

    private bool LooksLikeTree(Transform candidate)
    {
        return candidate != null && candidate.name.ToLowerInvariant().Contains("tree");
    }

    private float GetHorizontalScale(Transform target)
    {
        Vector3 scale = target.lossyScale;
        return (Mathf.Abs(scale.x) + Mathf.Abs(scale.z)) * 0.5f;
    }

    private void AddDashedRing(List<Vector3> vertices, List<int> triangles, Vector3 center, float radius, float width)
    {
        float angleStep = Mathf.PI * 2f / dashCount;
        float halfWidth = width * 0.5f;

        for (int i = 0; i < dashCount; i++)
        {
            float startAngle = i * angleStep;
            float endAngle = startAngle + angleStep * dashFill;
            Vector3 start = center + new Vector3(Mathf.Cos(startAngle) * radius, 0f, Mathf.Sin(startAngle) * radius);
            Vector3 end = center + new Vector3(Mathf.Cos(endAngle) * radius, 0f, Mathf.Sin(endAngle) * radius);
            Vector3 direction = (end - start).normalized;
            Vector3 side = new Vector3(-direction.z, 0f, direction.x) * halfWidth;

            int baseIndex = vertices.Count;
            vertices.Add(start - side);
            vertices.Add(start + side);
            vertices.Add(end - side);
            vertices.Add(end + side);

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 3);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
            triangles.Add(baseIndex + 1);
        }
    }

    private bool TryGetTreeBounds(Transform tree, out Bounds bounds)
    {
        Renderer[] renderers = tree.GetComponentsInChildren<Renderer>(true);
        bool foundRenderer = false;
        bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is LineRenderer)
            {
                continue;
            }

            if (!foundRenderer)
            {
                bounds = renderer.bounds;
                foundRenderer = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return foundRenderer;
    }

    private void CreateVisualRoot()
    {
        visualRoot = new GameObject(VisualRootName);
        visualRoot.transform.SetParent(transform, false);
        visualRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.NotEditable;
    }

    private Material GetVisualMaterial()
    {
        if (visualMaterial != null)
        {
            return visualMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        visualMaterial = new Material(shader)
        {
            name = "Tree Chop Range Material",
            hideFlags = HideFlags.HideAndDontSave,
        };

        SetMaterialColor(visualMaterial, rangeColor);
        SetMaterialFloat(visualMaterial, "_Cull", 0f);
        SetMaterialFloat(visualMaterial, "_ZWrite", 0f);
        visualMaterial.renderQueue = 3000;
        return visualMaterial;
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private void SetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private void CleanupVisuals()
    {
        GameObject existing = transform.Find(VisualRootName)?.gameObject;
        if (existing != null)
        {
            DestroyGeneratedObject(existing);
        }

        visualRoot = null;

        if (visualMesh != null)
        {
            DestroyGeneratedObject(visualMesh);
            visualMesh = null;
        }

        if (visualMaterial != null)
        {
            DestroyGeneratedObject(visualMaterial);
            visualMaterial = null;
        }
    }

    private void DestroyGeneratedObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void QueueRebuild()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (Application.isPlaying)
        {
            RebuildVisuals();
            return;
        }

#if UNITY_EDITOR
        if (editorRebuildQueued)
        {
            return;
        }

        editorRebuildQueued = true;
        EditorApplication.delayCall += DelayedEditorRebuild;
#endif
    }

    private void FindPlayerIfNeeded()
    {
        if (!autoFindPlayer || player != null)
        {
            return;
        }

        if (Time.time < nextPlayerSearchTime)
        {
            return;
        }

        nextPlayerSearchTime = Time.time + 1f;
        player = FindObjectOfType<CatPlayerController>();
    }

    private bool IsPlayerInsideAnyRange(Vector3 playerPosition)
    {
        Vector2 playerXZ = new Vector2(playerPosition.x, playerPosition.z);
        for (int i = 0; i < treeRanges.Count; i++)
        {
            TreeRange treeRange = treeRanges[i];
            Vector2 centerXZ = new Vector2(treeRange.center.x, treeRange.center.z);
            if ((playerXZ - centerXZ).sqrMagnitude <= treeRange.radius * treeRange.radius)
            {
                return true;
            }
        }

        return false;
    }

    private void AddWood()
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogWarning("TreesChoppingSystem cannot find ResourceManager.");
            return;
        }

        ResourceManager.Instance.Add(ResourceType.Wood, woodPerTick);
    }

    private void RebuildIfTransformChanged()
    {
        if (rebuilding)
        {
            return;
        }

        if (transform.position == lastPosition
            && transform.rotation == lastRotation
            && transform.lossyScale == lastScale)
        {
            return;
        }

        QueueRebuild();
    }

    private void CacheTransformState()
    {
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastScale = transform.lossyScale;
    }

#if UNITY_EDITOR
    private void DelayedEditorRebuild()
    {
        editorRebuildQueued = false;
        if (this == null || !isActiveAndEnabled || Application.isPlaying)
        {
            return;
        }

        RebuildVisuals();
        SceneView.RepaintAll();
    }
#endif
}
