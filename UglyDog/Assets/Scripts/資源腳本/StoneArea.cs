using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class StoneArea : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private CatPlayerController player;
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField] private string preferredPlayerName = PreferredPlayerFinder.PreferredPlayerName;

    [Header("Interaction Area")]
    [SerializeField] private Collider interactionArea;
    [SerializeField] private bool autoUseParentTriggerArea = true;

    [Header("Mining")]
    [SerializeField] private int stonePerTick = 1;
    [SerializeField] private float tickInterval = 1f;
    [SerializeField] private bool playDigAnimation = true;
    [SerializeField] private bool requirePlayerStopped = true;
    [SerializeField] private float stoppedInputThreshold = 0.05f;

    [Header("Range Visual Only")]
    [SerializeField] private float mineRange = 2.2f;
    [SerializeField] private float rangePadding = 0.65f;
    [SerializeField] private Vector3 rangeCenterOffset;
    [SerializeField] private Color rangeColor = new Color(0.55f, 0.55f, 0.55f, 0.95f);
    [SerializeField] private int dashCount = 24;
    [SerializeField] private float dashFill = 0.55f;
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private float ringHeight = 0.16f;
    [SerializeField] private bool autoGenerateRangeVisuals;

    private const string VisualRootName = "Stone Area Range Visuals";

    private readonly List<StoneRange> stoneRanges = new List<StoneRange>();
    private GameObject visualRoot;
    private Mesh visualMesh;
    private Material visualMaterial;
    private bool rebuilding;
    private bool playerWasInRange;
    private bool playerWasGathering;
    private float nextGatherTime;
    private float nextPlayerSearchTime;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 lastScale;
#if UNITY_EDITOR
    private bool editorRebuildQueued;
#endif

    private struct StoneRange
    {
        public Vector3 center;
        public float radius;
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            if (autoGenerateRangeVisuals)
            {
                RebuildIfTransformChanged();
            }

            return;
        }

        if (stoneRanges.Count == 0)
        {
            if (autoGenerateRangeVisuals)
            {
                RebuildVisuals();
            }
            else
            {
                RebuildRangeCache();
            }
        }

        FindPlayerIfNeeded();
        FindInteractionAreaIfNeeded();

        bool playerInRange = player != null && IsPlayerInsideInteractionArea(player.transform.position);
        bool playerCanGather = playerInRange && CanGatherNow(player);
        if (playerCanGather)
        {
            if (!playerWasGathering)
            {
                nextGatherTime = Time.time + GetEffectiveTickInterval();
            }

            if (playDigAnimation)
            {
                player.PlayDig();
            }

            if (Time.time >= nextGatherTime)
            {
                nextGatherTime = Time.time + GetEffectiveTickInterval();
                AddStone();
            }
        }
        else if (playerWasGathering && player != null && playDigAnimation)
        {
            player.StopAction();
        }

        playerWasInRange = playerInRange;
        playerWasGathering = playerCanGather;
    }

    private void OnEnable()
    {
        if (autoGenerateRangeVisuals && !Application.isPlaying)
        {
            QueueRebuild();
        }
        else if (!autoGenerateRangeVisuals)
        {
            RebuildRangeCache();
        }
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
            if (autoGenerateRangeVisuals)
            {
                QueueRebuild();
            }
            else
            {
                stoneRanges.Clear();
            }
        }
    }

    private void OnValidate()
    {
        mineRange = Mathf.Max(0.25f, mineRange);
        rangePadding = Mathf.Max(0f, rangePadding);
        stonePerTick = Mathf.Max(1, stonePerTick);
        tickInterval = Mathf.Max(0.05f, tickInterval);
        stoppedInputThreshold = Mathf.Max(0f, stoppedInputThreshold);
        dashCount = Mathf.Clamp(dashCount, 8, 64);
        dashFill = Mathf.Clamp(dashFill, 0.1f, 0.9f);
        lineWidth = Mathf.Max(0.01f, lineWidth);
        ringHeight = Mathf.Max(0.01f, ringHeight);
        preferredPlayerName = string.IsNullOrWhiteSpace(preferredPlayerName) ? PreferredPlayerFinder.PreferredPlayerName : preferredPlayerName.Trim();

        if (autoGenerateRangeVisuals && !Application.isPlaying)
        {
            QueueRebuild();
        }
        else if (!autoGenerateRangeVisuals)
        {
            stoneRanges.Clear();
        }
    }

    [ContextMenu("Rebuild Stone Area Visuals")]
    public void RebuildVisuals()
    {
        rebuilding = true;
        try
        {
            CleanupVisuals();
            CreateVisualRoot();
            CacheTransformState();

            stoneRanges.Clear();
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            if (LooksLikeStone(transform) && CountRenderableChildren() == 0)
            {
                AddRangeForStone(transform, vertices, triangles);
            }
            else
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform stone = transform.GetChild(i);
                    if (stone == null || stone.name == VisualRootName || !stone.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    AddRangeForStone(stone, vertices, triangles);
                }
            }

            visualMesh = new Mesh
            {
                name = "Stone Area Range Mesh",
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

    private void AddRangeForStone(Transform stone, List<Vector3> vertices, List<int> triangles)
    {
        Bounds bounds;
        if (!TryGetStoneBounds(stone, out bounds))
        {
            return;
        }

        float worldRadius = Mathf.Max(mineRange, Mathf.Max(bounds.extents.x, bounds.extents.z) + rangePadding);
        Vector3 anchor = stone.TransformPoint(rangeCenterOffset);
        Vector3 worldCenter = new Vector3(anchor.x, anchor.y + ringHeight, anchor.z);
        Vector3 localCenter = visualRoot.transform.InverseTransformPoint(worldCenter);
        float localScale = Mathf.Max(0.001f, GetHorizontalScale(visualRoot.transform));
        float localRadius = worldRadius / localScale;
        float localLineWidth = lineWidth / localScale;

        stoneRanges.Add(new StoneRange { center = worldCenter, radius = worldRadius });
        AddDashedRing(vertices, triangles, localCenter, localRadius, localLineWidth);
    }

    private void RebuildRangeCache()
    {
        stoneRanges.Clear();

        if (LooksLikeStone(transform) && CountRenderableChildren() == 0)
        {
            AddRangeCacheForStone(transform);
            return;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform stone = transform.GetChild(i);
            if (stone == null || stone.name == VisualRootName || !stone.gameObject.activeInHierarchy)
            {
                continue;
            }

            AddRangeCacheForStone(stone);
        }
    }

    private void AddRangeCacheForStone(Transform stone)
    {
        Bounds bounds;
        if (!TryGetStoneBounds(stone, out bounds))
        {
            return;
        }

        float worldRadius = Mathf.Max(mineRange, Mathf.Max(bounds.extents.x, bounds.extents.z) + rangePadding);
        Vector3 anchor = stone.TransformPoint(rangeCenterOffset);
        Vector3 worldCenter = new Vector3(anchor.x, anchor.y + ringHeight, anchor.z);
        stoneRanges.Add(new StoneRange { center = worldCenter, radius = worldRadius });
    }

    private bool LooksLikeStone(Transform candidate)
    {
        return candidate != null && candidate.name.ToLowerInvariant().Contains("stone");
    }

    private int CountRenderableChildren()
    {
        int count = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name != VisualRootName && child.GetComponentInChildren<Renderer>(true) != null)
            {
                count++;
            }
        }

        return count;
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

    private bool TryGetStoneBounds(Transform stone, out Bounds bounds)
    {
        Renderer[] renderers = stone.GetComponentsInChildren<Renderer>(true);
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
            name = "Stone Area Range Material",
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
        player = FindPreferredPlayer();
    }

    private CatPlayerController FindPreferredPlayer()
    {
        return PreferredPlayerFinder.FindPreferredPlayer();
    }

    private void FindInteractionAreaIfNeeded()
    {
        if (!autoUseParentTriggerArea || interactionArea != null)
        {
            return;
        }

        Collider bestArea = FindBestTriggerArea(transform);
        if (bestArea == null && transform.parent != null)
        {
            bestArea = FindBestTriggerArea(transform.parent);
        }

        if (bestArea != null)
        {
            interactionArea = bestArea;
        }
    }

    private Collider FindBestTriggerArea(Transform searchRoot)
    {
        Collider bestArea = null;
        int bestScore = int.MinValue;

        Collider[] parentColliders = GetComponentsInParent<Collider>(true);
        for (int i = 0; i < parentColliders.Length; i++)
        {
            ConsiderTriggerArea(parentColliders[i], ref bestArea, ref bestScore);
        }

        Collider[] childColliders = searchRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childColliders.Length; i++)
        {
            ConsiderTriggerArea(childColliders[i], ref bestArea, ref bestScore);
        }

        return bestArea;
    }

    private void ConsiderTriggerArea(Collider candidate, ref Collider bestArea, ref int bestScore)
    {
        if (candidate == null || !candidate.enabled || !candidate.isTrigger)
        {
            return;
        }

        int score = 0;
        string lowerName = candidate.gameObject.name.ToLowerInvariant();
        if (candidate.GetComponent<InteractiveAreaVisual>() != null)
        {
            score += 100;
        }

        if (lowerName.Contains("dashed"))
        {
            score += 40;
        }

        if (lowerName.Contains("area"))
        {
            score += 30;
        }

        if (candidate.transform == transform)
        {
            score += 10;
        }
        else if (candidate.transform.IsChildOf(transform))
        {
            score += 5;
        }

        if (score > bestScore)
        {
            bestScore = score;
            bestArea = candidate;
        }
    }

    private bool IsPlayerInsideInteractionArea(Vector3 playerPosition)
    {
        if (interactionArea != null && interactionArea.enabled)
        {
            return IsPointInsideAreaXZ(interactionArea, playerPosition);
        }

        if (HasManualInteractionArea())
        {
            return false;
        }

        return IsPlayerInsideAnyRange(playerPosition);
    }

    private bool CanGatherNow(CatPlayerController activePlayer)
    {
        return activePlayer != null
            && (!requirePlayerStopped || !activePlayer.HasMovementInput(stoppedInputThreshold));
    }

    private bool HasManualInteractionArea()
    {
        if (GetComponentInChildren<InteractiveAreaVisual>(true) != null)
        {
            return true;
        }

        return transform.parent != null && transform.parent.GetComponentInChildren<InteractiveAreaVisual>(true) != null;
    }

    private bool IsPointInsideAreaXZ(Collider area, Vector3 point)
    {
        BoxCollider box = area as BoxCollider;
        if (box != null)
        {
            Vector3 localPoint = box.transform.InverseTransformPoint(point) - box.center;
            Vector3 halfSize = box.size * 0.5f;
            return Mathf.Abs(localPoint.x) <= halfSize.x && Mathf.Abs(localPoint.z) <= halfSize.z;
        }

        Bounds bounds = area.bounds;
        return point.x >= bounds.min.x
            && point.x <= bounds.max.x
            && point.z >= bounds.min.z
            && point.z <= bounds.max.z;
    }

    private bool IsPlayerInsideAnyRange(Vector3 playerPosition)
    {
        Vector2 playerXZ = new Vector2(playerPosition.x, playerPosition.z);
        for (int i = 0; i < stoneRanges.Count; i++)
        {
            StoneRange stoneRange = stoneRanges[i];
            Vector2 centerXZ = new Vector2(stoneRange.center.x, stoneRange.center.z);
            if ((playerXZ - centerXZ).sqrMagnitude <= stoneRange.radius * stoneRange.radius)
            {
                return true;
            }
        }

        return false;
    }

    private void AddStone()
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogWarning("StoneArea cannot find ResourceManager.");
            return;
        }

        ResourceManager.Instance.Add(ResourceType.Stone, stonePerTick);
    }

    private float GetEffectiveTickInterval()
    {
        PlayerUpgradeManager upgrades = PlayerUpgradeManager.Instance;
        return upgrades != null ? upgrades.GetGatherTickInterval(ResourceType.Stone, tickInterval) : tickInterval;
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
