using UnityEngine;

public static class ArcadeMovementDemoBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BuildDemo()
    {
        if (Object.FindObjectOfType<PlayerMovementDemo>() != null)
        {
            return;
        }

        CreateGround();
        var player = CreatePlayer();
        CreateWoodGatherZone();
        CreateCamera(player.transform);
    }

    private static void CreateGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "DemoGround";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(2f, 1f, 2f);

        var material = new Material(Shader.Find("Standard"));
        material.color = new Color(0.82f, 0.92f, 0.82f);
        ground.GetComponent<Renderer>().material = material;
    }

    private static GameObject CreatePlayer()
    {
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "DemoPlayer";
        player.transform.position = new Vector3(0f, 1f, 0f);

        var controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.5f;
        controller.center = new Vector3(0f, 1f, 0f);
        controller.stepOffset = 0.2f;

        var movement = player.AddComponent<PlayerMovementDemo>();
        movement.moveSpeed = 6f;
        movement.rotationSpeed = 12f;
        movement.gravity = -20f;

        player.AddComponent<PlayerResourceInventory>();
        player.AddComponent<DemoHud>();

        var material = new Material(Shader.Find("Standard"));
        material.color = new Color(0.91f, 0.56f, 0.36f);
        player.GetComponent<Renderer>().material = material;

        return player;
    }

    private static void CreateWoodGatherZone()
    {
        var zoneRoot = new GameObject("WoodGatherZone");
        zoneRoot.transform.position = new Vector3(8f, 0f, 8f);

        var zoneVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        zoneVisual.name = "ZoneVisual";
        zoneVisual.transform.SetParent(zoneRoot.transform, false);
        zoneVisual.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        zoneVisual.transform.localScale = new Vector3(4f, 0.4f, 4f);

        var zoneMaterial = new Material(Shader.Find("Standard"));
        zoneMaterial.color = new Color(0.42f, 0.63f, 0.87f, 1f);
        zoneVisual.GetComponent<Renderer>().material = zoneMaterial;

        var triggerCollider = zoneRoot.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.center = new Vector3(0f, 1f, 0f);
        triggerCollider.size = new Vector3(4f, 2f, 4f);

        var gatherZone = zoneRoot.AddComponent<ResourceGatherZone>();
        gatherZone.resourceType = "Wood";
        gatherZone.gatherAmount = 5;
        gatherZone.gatherInterval = 1f;

        var tree = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tree.name = "TreeVisual";
        tree.transform.SetParent(zoneRoot.transform, false);
        tree.transform.localPosition = new Vector3(0f, 1f, 0f);
        tree.transform.localScale = new Vector3(0.7f, 1f, 0.7f);

        var treeMaterial = new Material(Shader.Find("Standard"));
        treeMaterial.color = new Color(0.29f, 0.56f, 0.3f, 1f);
        tree.GetComponent<Renderer>().material = treeMaterial;

        var treeCollider = tree.GetComponent<Collider>();
        if (treeCollider != null)
        {
            treeCollider.enabled = false;
        }
    }

    private static void CreateCamera(Transform target)
    {
        Camera cam;
        if (Camera.main != null)
        {
            cam = Camera.main;
        }
        else
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cam = cameraObject.AddComponent<Camera>();
        }

        var follow = cam.gameObject.GetComponent<SimpleFollowCamera>();
        if (follow == null)
        {
            follow = cam.gameObject.AddComponent<SimpleFollowCamera>();
        }

        follow.target = target;
        follow.offset = new Vector3(0f, 10f, -8f);
        follow.smoothTime = 0.12f;
    }
}

public class PlayerMovementDemo : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float rotationSpeed = 10f;
    public float gravity = -20f;

    private CharacterController _characterController;
    private Vector3 _velocity;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        var direction = new Vector3(input.x, 0f, input.y).normalized;

        if (direction.sqrMagnitude > 0f)
        {
            var lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
        }

        var movement = direction * moveSpeed;
        if (_characterController.isGrounded && _velocity.y < 0f)
        {
            _velocity.y = -1f;
        }

        _velocity.y += gravity * Time.deltaTime;

        _characterController.Move((movement + _velocity) * Time.deltaTime);
    }
}

public class PlayerResourceInventory : MonoBehaviour
{
    public int wood;

    public void AddWood(int amount)
    {
        wood += amount;
    }
}

public class ResourceGatherZone : MonoBehaviour
{
    public string resourceType = "Wood";
    public int gatherAmount = 5;
    public float gatherInterval = 1f;

    private float _nextGatherTime;

    private void OnTriggerStay(Collider other)
    {
        if (Time.time < _nextGatherTime)
        {
            return;
        }

        var inventory = other.GetComponent<PlayerResourceInventory>();
        if (inventory == null)
        {
            return;
        }

        if (resourceType == "Wood")
        {
            inventory.AddWood(gatherAmount);
        }

        _nextGatherTime = Time.time + gatherInterval;
    }
}

public class DemoHud : MonoBehaviour
{
    private PlayerResourceInventory _inventory;

    private void Awake()
    {
        _inventory = GetComponent<PlayerResourceInventory>();
    }

    private void OnGUI()
    {
        if (_inventory == null)
        {
            return;
        }

        GUI.Box(new Rect(12f, 12f, 320f, 90f), "醜力全開大亂鬥 - Demo");
        GUI.Label(new Rect(24f, 40f, 260f, 22f), "WASD 移動到藍色方框開始砍樹");
        GUI.Label(new Rect(24f, 62f, 220f, 22f), "木材: " + _inventory.wood);
    }
}

public class SimpleFollowCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 10f, -8f);
    public float smoothTime = 0.1f;

    private Vector3 _currentVelocity;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        var desiredPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _currentVelocity, smoothTime);
        transform.LookAt(target.position + Vector3.up * 1.1f);
    }
}
