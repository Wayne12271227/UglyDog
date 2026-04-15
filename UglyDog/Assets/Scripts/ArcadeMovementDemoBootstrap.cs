using UnityEngine;

public static class ArcadeMovementDemoBootstrap
{
    private const string PrefabRoot = "DemoPrefabs/";
    private const string GroundPrefabName = "DemoGround";
    private const string PlayerPrefabName = "DemoPlayer";
    private const string WoodZonePrefabName = "WoodGatherZone";
    private const string CameraPrefabName = "DemoCamera";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BuildDemo()
    {
        if (Object.FindObjectOfType<PlayerMovementDemo>() != null)
        {
            return;
        }

        var groundPrefab = LoadPrefab(GroundPrefabName);
        var playerPrefab = LoadPrefab(PlayerPrefabName);
        var woodZonePrefab = LoadPrefab(WoodZonePrefabName);
        var cameraPrefab = LoadPrefab(CameraPrefabName);

        if (groundPrefab == null || playerPrefab == null || woodZonePrefab == null || cameraPrefab == null)
        {
            Debug.LogWarning("Demo prefabs are missing. In Unity, run Tools/UglyDog/Create Demo Prefabs to generate them.");
            return;
        }

        Object.Instantiate(groundPrefab, Vector3.zero, Quaternion.identity);

        var playerObject = Object.Instantiate(playerPrefab, new Vector3(0f, 1f, 0f), Quaternion.identity);

        Object.Instantiate(woodZonePrefab, new Vector3(8f, 0f, 8f), Quaternion.identity);

        var cameraObject = Object.Instantiate(cameraPrefab, Vector3.zero, Quaternion.identity);
        var followCamera = cameraObject.GetComponent<SimpleFollowCamera>();
        if (followCamera != null)
        {
            followCamera.target = playerObject.transform;
        }
    }

    private static GameObject LoadPrefab(string prefabName)
    {
        return Resources.Load<GameObject>(PrefabRoot + prefabName);
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

        inventory.AddWood(gatherAmount);
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
