using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(BoxCollider))]
[ExecuteAlways]
public class UpgradeShopZone : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.E;
    [SerializeField] private bool requirePlayerController = true;
    [SerializeField] private string promptText = "\u6309 E \u958b\u555f\u5546\u5e97";
    [SerializeField] private Vector3 promptLocalOffset = new Vector3(0f, 1.9f, 0f);

    [Header("Minion Shop")]
    [SerializeField] private bool enableMinionHotkeys = true;
    [SerializeField] private KeyCode buyMeleeMinionKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode buyRangedMinionKey = KeyCode.Alpha2;

    [Header("Range")]
    [SerializeField] private Vector3 rangeCenter = new Vector3(0f, 1f, 0f);
    [SerializeField] private Vector3 rangeSize = new Vector3(4f, 2.4f, 4f);

    [Header("Range Visual")]
    [SerializeField] private bool showRangeVisual = true;
    [SerializeField] private Color rangeColor = new Color(1f, 0.74f, 0.18f, 0.95f);
    [SerializeField] private Color catRangeColor = new Color(0.55f, 0.55f, 0.55f, 0.95f);
    [SerializeField] private float rangeLineWidth = 0.08f;
    [SerializeField] private float rangeVisualHeight = 0.08f;
    [SerializeField] private int rangeSegments = 96;

    private Collider zoneCollider;
    private CatPlayerController activePlayer;
    private int playersInside;
    private TextMesh promptMesh;
    private GameObject promptObject;
    private Transform promptTarget;
    private float nextInsideCheckTime;
    private string minionFlashText;
    private float minionFlashUntil;

    private const string RangeVisualName = "Shop Interaction Range Visual";
    private const string ShopRootObjectName = "dogShop";

    private void Reset()
    {
        zoneCollider = EnsureZoneCollider();
        RefreshRangeVisual();
    }

    private void Awake()
    {
        zoneCollider = EnsureZoneCollider();
        RefreshRangeVisual();
    }

    private void OnEnable()
    {
        zoneCollider = EnsureZoneCollider();
        RefreshRangeVisual();
    }

    private void OnValidate()
    {
        rangeSize.x = Mathf.Max(0.1f, rangeSize.x);
        rangeSize.y = Mathf.Max(0.1f, rangeSize.y);
        rangeSize.z = Mathf.Max(0.1f, rangeSize.z);
        rangeLineWidth = Mathf.Max(0.005f, rangeLineWidth);
        rangeVisualHeight = Mathf.Max(0.001f, rangeVisualHeight);
        rangeSegments = Mathf.Clamp(rangeSegments, 16, 192);

        zoneCollider = EnsureZoneCollider();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }

                zoneCollider = EnsureZoneCollider();
                RefreshRangeVisual();
            };
            return;
        }
#endif
        RefreshRangeVisual();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (BuildingPlacementController.BlocksPlayerInput)
        {
            HidePrompt();
            return;
        }

        CatPlayerController playerInRange = GetPreferredPlayerInsideZone();
        if (playerInRange == null)
        {
            activePlayer = null;
            playersInside = 0;
            HidePrompt();
            return;
        }

        activePlayer = playerInRange;
        playersInside = Mathf.Max(1, playersInside);
        UpdatePrompt();
        if (promptObject == null || !promptObject.activeSelf)
        {
            ShowPrompt(playerInRange);
        }

        if (Input.GetKeyDown(toggleKey))
        {
            if (GetPreferredPlayerInsideZone() != null)
            {
                UpgradeShopUI.EnsureInstance().Toggle(this);
                HidePrompt();
            }
        }

        if (enableMinionHotkeys && GetPreferredPlayerInsideZone() != null)
        {
            if (Input.GetKeyDown(buyMeleeMinionKey))
            {
                TryBuyMinion(MinionKind.Melee);
            }
            else if (Input.GetKeyDown(buyRangedMinionKey))
            {
                TryBuyMinion(MinionKind.Ranged);
            }
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
        if (activePlayer == null)
        {
            activePlayer = player;
        }

        ShowPrompt(player);
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

        UpgradeShopUI ui = UpgradeShopUI.Instance;
        if (ui != null)
        {
            ui.CloseIfOpenedBy(this);
        }

        if (playersInside <= 0)
        {
            HidePrompt();
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
        return PreferredPlayerFinder.GetPlayer(other, GetShopTeam());
    }

    private CatPlayerController GetPreferredPlayerInsideZone()
    {
        if (zoneCollider == null)
        {
            zoneCollider = EnsureZoneCollider();
        }

        if (activePlayer != null && IsInsideZone(activePlayer.transform.position))
        {
            return activePlayer;
        }

        if (Time.time < nextInsideCheckTime)
        {
            return null;
        }

        nextInsideCheckTime = Time.time + 0.08f;
        CatPlayerController player = PreferredPlayerFinder.FindPlayer(GetShopTeam());
        if (player != null && IsInsideZone(player.transform.position))
        {
            return player;
        }

        return null;
    }

    private bool IsInsideZone(Vector3 worldPosition)
    {
        Vector3 closest = zoneCollider.ClosestPoint(worldPosition);
        return (closest - worldPosition).sqrMagnitude <= 0.0001f;
    }

    private Collider EnsureZoneCollider()
    {
        Collider[] colliders = GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].isTrigger)
            {
                return colliders[i];
            }
        }

        BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        ApplyBoxRange(trigger);
        return trigger;
    }

    public void RefreshRangeVisual()
    {
        CleanupLegacyChildVisual();
        if (IsShopRootObject())
        {
            return;
        }

        if (zoneCollider == null)
        {
            zoneCollider = EnsureZoneCollider();
        }

        if (zoneCollider is BoxCollider boxCollider)
        {
            ApplyBoxRange(boxCollider);
        }

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (!showRangeVisual)
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }

            return;
        }

        LineRenderer oldLine = GetComponent<LineRenderer>();
        if (oldLine != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(oldLine);
            }
            else
#endif
            {
                Destroy(oldLine);
            }
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        meshRenderer.enabled = true;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        Material material = meshRenderer.sharedMaterial;
        string materialName = GetRangeVisualMaterialName();
        if (material == null || !material.name.StartsWith(materialName))
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                material = new Material(shader);
                material.name = materialName;
                meshRenderer.sharedMaterial = material;
            }
        }

        if (material != null)
        {
            Color effectiveRangeColor = GetRangeVisualColor();
            SetMaterialColor(material, effectiveRangeColor);
            material.color = effectiveRangeColor;
            material.renderQueue = 3000;
        }

        Mesh previousMesh = meshFilter.sharedMesh;
        meshFilter.sharedMesh = BuildSolidRangeRing();
        if (previousMesh != null && previousMesh.name == "Dog Shop Solid Range Ring")
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(previousMesh);
            }
            else
#endif
            {
                Destroy(previousMesh);
            }
        }
    }

    private Mesh BuildSolidRangeRing()
    {
        int vertexCount = rangeSegments * 2;
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[rangeSegments * 6];

        float outerRadiusX = Mathf.Max(0.05f, rangeSize.x * 0.5f);
        float outerRadiusZ = Mathf.Max(0.05f, rangeSize.z * 0.5f);
        float innerRadiusX = Mathf.Max(0.01f, outerRadiusX - rangeLineWidth);
        float innerRadiusZ = Mathf.Max(0.01f, outerRadiusZ - rangeLineWidth);

        for (int i = 0; i < rangeSegments; i++)
        {
            float angle = i / (float)rangeSegments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices[i * 2] = new Vector3(rangeCenter.x + cos * outerRadiusX, rangeVisualHeight, rangeCenter.z + sin * outerRadiusZ);
            vertices[i * 2 + 1] = new Vector3(rangeCenter.x + cos * innerRadiusX, rangeVisualHeight, rangeCenter.z + sin * innerRadiusZ);
        }

        for (int i = 0; i < rangeSegments; i++)
        {
            int next = (i + 1) % rangeSegments;
            int triangleIndex = i * 6;
            int outerA = i * 2;
            int innerA = outerA + 1;
            int outerB = next * 2;
            int innerB = outerB + 1;

            triangles[triangleIndex] = outerA;
            triangles[triangleIndex + 1] = outerB;
            triangles[triangleIndex + 2] = innerA;
            triangles[triangleIndex + 3] = innerA;
            triangles[triangleIndex + 4] = outerB;
            triangles[triangleIndex + 5] = innerB;
        }

        Mesh mesh = new Mesh
        {
            name = "Dog Shop Solid Range Ring",
        };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private void CleanupLegacyChildVisual()
    {
        Transform legacyVisual = transform.Find(RangeVisualName);
        if (legacyVisual == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(legacyVisual.gameObject);
            return;
        }
#endif
        Destroy(legacyVisual.gameObject);
    }

    private bool IsShopRootObject()
    {
        return name.Equals(ShopRootObjectName, System.StringComparison.OrdinalIgnoreCase)
            || name.Equals("catShop", System.StringComparison.OrdinalIgnoreCase);
    }

    private void SetMaterialColor(Material material, Color color)
    {
        material.SetColor("_Color", color);
        material.SetColor("_BaseColor", color);
        material.SetColor("_EmissionColor", color);
    }

    private void ApplyBoxRange(BoxCollider boxCollider)
    {
        boxCollider.isTrigger = true;
        boxCollider.center = rangeCenter;
        boxCollider.size = rangeSize;
    }

    private void ShowPrompt(CatPlayerController player)
    {
        if (player == null)
        {
            return;
        }

        if (promptObject == null)
        {
            promptObject = new GameObject("Shop Prompt");
            promptMesh = promptObject.AddComponent<TextMesh>();
            Font promptFont = LoadPromptFont();
            if (promptFont != null)
            {
                promptMesh.font = promptFont;
                MeshRenderer promptRenderer = promptObject.GetComponent<MeshRenderer>();
                if (promptRenderer != null)
                {
                    promptRenderer.sharedMaterial = promptFont.material;
                }
            }

            promptMesh.anchor = TextAnchor.MiddleCenter;
            promptMesh.alignment = TextAlignment.Center;
            promptMesh.characterSize = 0.14f;
            promptMesh.fontSize = 32;
            promptMesh.color = Color.white;
        }

        promptTarget = player.transform;
        promptObject.transform.SetParent(null, true);
        promptObject.SetActive(true);
        promptMesh.text = GetCurrentPromptText();
        UpdatePrompt();
    }

    private void HidePrompt()
    {
        if (promptObject != null)
        {
            promptObject.SetActive(false);
        }

        promptTarget = null;
    }

    private void UpdatePrompt()
    {
        if (promptObject == null || !promptObject.activeSelf)
        {
            return;
        }

        if (UpgradeShopUI.BlocksPlayerInput)
        {
            HidePrompt();
            return;
        }

        if (promptTarget != null)
        {
            promptObject.transform.position = promptTarget.TransformPoint(promptLocalOffset);
        }

        if (promptMesh != null)
        {
            promptMesh.text = GetCurrentPromptText();
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

    private Font LoadPromptFont()
    {
        Font font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft JhengHei", "Microsoft YaHei", "Arial Unicode MS", "Noto Sans CJK TC" },
            18);

        if (font != null)
        {
            return font;
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void TryBuyMinion(MinionKind kind)
    {
        MinionManager manager = MinionManager.EnsureInstance();
        MinionTeam team = GetShopTeam();
        bool bought = manager.TryBuyAndSummon(kind, team);
        if (bought)
        {
            FlashMinionPrompt("\u5df2\u53ec\u559a " + manager.GetDisplayName(kind));
        }
        else
        {
            FlashMinionPrompt("\u9700\u8981 " + manager.GetCost(kind) + " \u91d1\u5e63");
        }
    }

    private void FlashMinionPrompt(string text)
    {
        minionFlashText = text;
        minionFlashUntil = Time.time + 1.1f;
        if (promptMesh != null)
        {
            promptMesh.text = text;
        }
    }

    private string GetCurrentPromptText()
    {
        if (Time.time < minionFlashUntil && !string.IsNullOrEmpty(minionFlashText))
        {
            return minionFlashText;
        }

        if (!enableMinionHotkeys)
        {
            return promptText;
        }

        MinionManager manager = MinionManager.EnsureInstance();
        return promptText
            + "\n1 " + manager.GetDisplayName(MinionKind.Melee) + " -" + manager.GetCost(MinionKind.Melee) + " \u91d1\u5e63"
            + "\n2 " + manager.GetDisplayName(MinionKind.Ranged) + " -" + manager.GetCost(MinionKind.Ranged) + " \u91d1\u5e63";
    }

    private MinionTeam GetShopTeam()
    {
        string lowerName = GetHierarchyName(transform).ToLowerInvariant();
        return lowerName.Contains("cat") ? MinionTeam.Cat : MinionTeam.Dog;
    }

    private Color GetRangeVisualColor()
    {
        return GetShopTeam() == MinionTeam.Cat ? catRangeColor : rangeColor;
    }

    private string GetRangeVisualMaterialName()
    {
        return GetShopTeam() == MinionTeam.Cat ? "CatShopRangeVisual" : "DogShopRangeVisual";
    }

    private static string GetHierarchyName(Transform target)
    {
        string names = string.Empty;
        Transform current = target;
        while (current != null)
        {
            names += " " + current.name;
            current = current.parent;
        }

        return names;
    }
}
