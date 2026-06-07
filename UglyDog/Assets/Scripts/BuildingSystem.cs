using UnityEngine;

public enum BuildingType
{
    AutoLumberCamp,
    AutoQuarry
}

public static class BuildingSystem
{
    private const int DefaultBuildingHealth = 20;

    public static bool CanBuy(BuildingType type)
    {
        ResourceManager resources = ResourceManager.Instance;
        return resources != null && resources.CanSpend(ResourceType.Coin, GetCoinCost(type));
    }

    public static bool BeginPlacement(BuildingType type)
    {
        if (!CanBuy(type))
        {
            return false;
        }

        BuildingPlacementController.EnsureInstance().BeginPlacement(type);
        return true;
    }

    public static bool TryPlacePurchasedBuilding(BuildingType type, Vector3 position)
    {
        ResourceManager resources = ResourceManager.Instance;
        if (resources == null || !resources.Spend(ResourceType.Coin, GetCoinCost(type)))
        {
            return false;
        }

        CreateBuilding(type, position);
        return true;
    }

    public static string GetDisplayName(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.AutoLumberCamp:
                return "自動伐木場";
            case BuildingType.AutoQuarry:
                return "自動採石場";
            default:
                return "建築物";
        }
    }

    public static string GetEffectText(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.AutoLumberCamp:
                return "\u6bcf 2 \u79d2 +1 \u6728\u982d";
            case BuildingType.AutoQuarry:
                return "\u6bcf 2 \u79d2 +1 \u77f3\u982d";
            default:
                return string.Empty;
        }
    }

    public static int GetCoinCost(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.AutoLumberCamp:
                return 80;
            case BuildingType.AutoQuarry:
                return 120;
            default:
                return 0;
        }
    }

    public static Vector3 GetFootprintSize(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.AutoLumberCamp:
                return new Vector3(2.2f, 1.8f, 2.2f);
            case BuildingType.AutoQuarry:
                return new Vector3(2.4f, 1.8f, 2.4f);
            default:
                return new Vector3(2f, 1.8f, 2f);
        }
    }

    private static void CreateBuilding(BuildingType type, Vector3 position)
    {
        GameObject root = new GameObject(GetDisplayName(type));
        root.transform.position = position;

        BuildingHealth health = root.AddComponent<BuildingHealth>();
        health.Configure(DefaultBuildingHealth);

        TeamBuilding teamBuilding = root.AddComponent<TeamBuilding>();
        teamBuilding.Configure(MinionTeam.Dog);

        AutoResourceBuilding producer = root.AddComponent<AutoResourceBuilding>();
        producer.Configure(GetProducedResource(type), 1, 2f);

        BoxCollider collider = root.AddComponent<BoxCollider>();
        Vector3 footprint = GetFootprintSize(type);
        collider.size = footprint;
        collider.center = new Vector3(0f, footprint.y * 0.5f, 0f);
        collider.isTrigger = true;

        CreateVisuals(root.transform, type);
    }

    private static ResourceType GetProducedResource(BuildingType type)
    {
        return type == BuildingType.AutoQuarry ? ResourceType.Stone : ResourceType.Wood;
    }

    private static void CreateVisuals(Transform parent, BuildingType type)
    {
        Color bodyColor = type == BuildingType.AutoQuarry
            ? new Color(0.46f, 0.49f, 0.52f, 1f)
            : new Color(0.48f, 0.28f, 0.12f, 1f);
        Color roofColor = type == BuildingType.AutoQuarry
            ? new Color(0.28f, 0.30f, 0.34f, 1f)
            : new Color(0.18f, 0.46f, 0.18f, 1f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(parent, false);
        body.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        body.transform.localScale = new Vector3(1.6f, 1.3f, 1.6f);
        Object.Destroy(body.GetComponent<Collider>());
        SetMaterial(body, bodyColor);

        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        roof.name = "Roof";
        roof.transform.SetParent(parent, false);
        roof.transform.localPosition = new Vector3(0f, 1.45f, 0f);
        roof.transform.localScale = new Vector3(1.05f, 0.24f, 1.05f);
        Object.Destroy(roof.GetComponent<Collider>());
        SetMaterial(roof, roofColor);

        GameObject marker = GameObject.CreatePrimitive(type == BuildingType.AutoQuarry ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
        marker.name = "Resource Marker";
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = new Vector3(0f, 1.85f, 0f);
        marker.transform.localScale = type == BuildingType.AutoQuarry
            ? Vector3.one * 0.42f
            : new Vector3(0.24f, 0.45f, 0.24f);
        Object.Destroy(marker.GetComponent<Collider>());
        SetMaterial(marker, type == BuildingType.AutoQuarry ? Color.gray : new Color(0.2f, 0.75f, 0.18f, 1f));
    }

    private static void SetMaterial(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;
        material.SetColor("_BaseColor", color);
        renderer.sharedMaterial = material;
    }
}
