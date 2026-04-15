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

        var material = new Material(Shader.Find("Standard"));
        material.color = new Color(0.91f, 0.56f, 0.36f);
        player.GetComponent<Renderer>().material = material;

        return player;
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
