using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingPlacementController : MonoBehaviour
{
    public static BuildingPlacementController Instance { get; private set; }
    public static bool BlocksPlayerInput => Instance != null && Instance.IsPlacing;

    [SerializeField] private LayerMask placementCollisionLayers = ~0;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private Color validColor = new Color(0.15f, 0.95f, 0.25f, 0.45f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.15f, 0.1f, 0.55f);
    [SerializeField] private float previewHeightOffset = 0.05f;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
    [SerializeField] private Vector3 promptLocalOffset = new Vector3(0f, 2.2f, 0f);

    private BuildingType pendingType;
    private GameObject previewObject;
    private Material previewMaterial;
    private GameObject promptObject;
    private TextMesh promptText;
    private Vector3 currentPosition;
    private Collider currentGroundCollider;
    private bool canPlace;

    public bool IsPlacing => previewObject != null;

    public static BuildingPlacementController EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        BuildingPlacementController existing = FindObjectOfType<BuildingPlacementController>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject controllerObject = new GameObject("Building Placement Controller");
        return controllerObject.AddComponent<BuildingPlacementController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!IsPlacing)
        {
            return;
        }

        UpdatePreview();
        UpdatePrompt();

        if (Input.GetKeyDown(cancelKey))
        {
            CancelPlacement();
            return;
        }

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            TryConfirmPlacement();
        }
    }

    public void BeginPlacement(BuildingType type)
    {
        CancelPlacement();
        pendingType = type;
        CreatePreview(type);
        ShowPrompt();
        UpdatePreview();
    }

    public void CancelPlacement()
    {
        if (previewObject != null)
        {
            Destroy(previewObject);
        }

        previewObject = null;
        previewMaterial = null;
        canPlace = false;
        HidePrompt();
    }

    private void TryConfirmPlacement()
    {
        if (!canPlace)
        {
            return;
        }

        if (BuildingSystem.TryPlacePurchasedBuilding(pendingType, currentPosition))
        {
            CancelPlacement();
        }
    }

    private void CreatePreview(BuildingType type)
    {
        Vector3 footprint = BuildingSystem.GetFootprintSize(type);

        previewObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        previewObject.name = BuildingSystem.GetDisplayName(type) + " Preview";
        previewObject.transform.localScale = new Vector3(footprint.x, 0.12f, footprint.z);

        Collider previewCollider = previewObject.GetComponent<Collider>();
        if (previewCollider != null)
        {
            Destroy(previewCollider);
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        previewMaterial = new Material(shader);
        previewMaterial.name = "Building Preview Material";
        SetMaterialTransparent(previewMaterial);

        Renderer renderer = previewObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = previewMaterial;
        }
    }

    private void UpdatePreview()
    {
        currentPosition = GetPointerWorldPosition();
        if (previewObject != null)
        {
            previewObject.transform.position = currentPosition + Vector3.up * previewHeightOffset;
        }

        canPlace = IsPlacementClear(currentPosition);
        if (previewMaterial != null)
        {
            Color color = canPlace ? validColor : invalidColor;
            previewMaterial.color = color;
            previewMaterial.SetColor("_BaseColor", color);
        }
    }

    private Vector3 GetPointerWorldPosition()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            CatPlayerController player = PreferredPlayerFinder.FindPreferredPlayer();
            return player != null ? player.transform.position + player.transform.forward * 3f : Vector3.zero;
        }

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            currentGroundCollider = HasGroundSurfaceName(hit.collider) ? hit.collider : null;
            return hit.point;
        }

        currentGroundCollider = null;
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        CatPlayerController fallbackPlayer = PreferredPlayerFinder.FindPreferredPlayer();
        return fallbackPlayer != null ? fallbackPlayer.transform.position + fallbackPlayer.transform.forward * 3f : Vector3.zero;
    }

    private bool IsPlacementClear(Vector3 position)
    {
        Vector3 footprint = BuildingSystem.GetFootprintSize(pendingType);
        Vector3 halfExtents = new Vector3(footprint.x * 0.5f, Mathf.Max(0.5f, footprint.y * 0.5f), footprint.z * 0.5f);
        Vector3 center = position + new Vector3(0f, halfExtents.y, 0f);
        Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, placementCollisionLayers, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (previewObject != null && hit.transform.IsChildOf(previewObject.transform))
            {
                continue;
            }

            if (PreferredPlayerFinder.GetPreferredPlayer(hit) != null)
            {
                continue;
            }

            if (hit.GetComponentInParent<CatPlayerController>() != null)
            {
                continue;
            }

            if (IsGroundSurfaceCollider(hit, position))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private bool IsGroundSurfaceCollider(Collider hit, Vector3 position)
    {
        if (hit == null || hit.isTrigger)
        {
            return false;
        }

        if (currentGroundCollider != null
            && (hit == currentGroundCollider
                || hit.transform.IsChildOf(currentGroundCollider.transform)
                || currentGroundCollider.transform.IsChildOf(hit.transform)))
        {
            return true;
        }

        return HasGroundSurfaceName(hit);
    }

    private bool HasGroundSurfaceName(Collider hit)
    {
        if (hit == null)
        {
            return false;
        }

        Transform current = hit.transform;
        while (current != null)
        {
            string lowerName = current.name.ToLowerInvariant();
            if (lowerName.Contains("ground")
                || lowerName.Contains("floor")
                || lowerName.Contains("terrain")
                || lowerName.Contains("grass")
                || lowerName.Contains("path"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void ShowPrompt()
    {
        if (promptObject == null)
        {
            promptObject = new GameObject("Building Placement Prompt");
            promptText = promptObject.AddComponent<TextMesh>();
            promptText.text = "按ESC取消建築";
            promptText.anchor = TextAnchor.MiddleCenter;
            promptText.alignment = TextAlignment.Center;
            promptText.characterSize = 0.14f;
            promptText.fontSize = 32;
            promptText.color = Color.white;
        }

        promptObject.SetActive(true);
        UpdatePrompt();
    }

    private void HidePrompt()
    {
        if (promptObject != null)
        {
            promptObject.SetActive(false);
        }
    }

    private void UpdatePrompt()
    {
        if (promptObject == null || !promptObject.activeSelf)
        {
            return;
        }

        CatPlayerController player = PreferredPlayerFinder.FindPreferredPlayer();
        if (player != null)
        {
            promptObject.transform.position = player.transform.TransformPoint(promptLocalOffset);
        }

        Camera camera = Camera.main;
        if (camera != null)
        {
            Vector3 direction = promptObject.transform.position - camera.transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                promptObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private static void SetMaterialTransparent(Material material)
    {
        if (material == null)
        {
            return;
        }

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
    }
}
