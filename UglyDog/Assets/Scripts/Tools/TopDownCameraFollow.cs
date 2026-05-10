using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class TopDownCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 8f, -6.5f);
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, 0.8f, 0f);
    [SerializeField] private float followSmoothTime = 0.12f;
    [SerializeField] private float fieldOfView = 42f;
    [SerializeField] private bool autoFindPlayer = true;

    [Header("Orbit")]
    [SerializeField] private bool allowOrbit = true;
    [SerializeField] private int mouseOrbitButton = 1;
    [SerializeField] private int resetViewMouseButton = 2;
    [SerializeField] private float mouseYawSpeed = 180f;
    [SerializeField] private float mousePitchSpeed = 90f;
    [SerializeField] private float minPitch = 20f;
    [SerializeField] private float maxPitch = 75f;

    [Header("Zoom")]
    [SerializeField] private bool allowZoom = true;
    [SerializeField] private float zoomSpeed = 3f;
    [SerializeField] private float minDistance = 4f;
    [SerializeField] private float maxDistance = 14f;

    private Camera cameraComponent;
    private Vector3 followVelocity;
    private float yaw;
    private float pitch;
    private float distance;
    private float defaultYaw;
    private float defaultPitch;
    private float defaultDistance;

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        InitializeOrbitFromOffset();
        ApplyCameraSettings();
        FindPlayerIfNeeded();
        SnapToTarget();
    }

    private void OnValidate()
    {
        cameraComponent = GetComponent<Camera>();
        InitializeOrbitFromOffset();
        ApplyCameraSettings();
        SnapToTarget();
    }

    private void LateUpdate()
    {
        FindPlayerIfNeeded();
        UpdateOrbitInput();

        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = GetTargetCameraPosition();

        if (Application.isPlaying)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref followVelocity, followSmoothTime);
        }
        else
        {
            transform.position = targetPosition;
        }

        LookAtTarget();
    }

    public void SnapToTarget()
    {
        FindPlayerIfNeeded();

        if (target == null)
        {
            return;
        }

        transform.position = GetTargetCameraPosition();
        LookAtTarget();
    }

    private Vector3 GetTargetCameraPosition()
    {
        return target.position + GetOrbitOffset();
    }

    private void LookAtTarget()
    {
        Vector3 lookPoint = target.position + lookOffset;
        Vector3 direction = lookPoint - transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }

    private void ApplyCameraSettings()
    {
        if (cameraComponent == null)
        {
            return;
        }

        cameraComponent.orthographic = false;
        cameraComponent.fieldOfView = fieldOfView;
    }

    private void InitializeOrbitFromOffset()
    {
        Vector3 startOffset = offset;
        if (startOffset.sqrMagnitude <= 0.001f)
        {
            startOffset = new Vector3(0f, 8f, -6.5f);
        }

        distance = Mathf.Clamp(startOffset.magnitude, minDistance, maxDistance);
        yaw = Mathf.Atan2(startOffset.x, startOffset.z) * Mathf.Rad2Deg;

        float horizontalDistance = new Vector2(startOffset.x, startOffset.z).magnitude;
        pitch = Mathf.Atan2(startOffset.y, horizontalDistance) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        defaultYaw = yaw;
        defaultPitch = pitch;
        defaultDistance = distance;
    }

    private void UpdateOrbitInput()
    {
        if (!Application.isPlaying || !allowOrbit)
        {
            return;
        }

        if (Input.GetMouseButtonDown(resetViewMouseButton))
        {
            ResetView();
        }

        if (Input.GetMouseButton(mouseOrbitButton))
        {
            yaw += Input.GetAxis("Mouse X") * mouseYawSpeed * Time.deltaTime;
            pitch -= Input.GetAxis("Mouse Y") * mousePitchSpeed * Time.deltaTime;
        }

        if (allowZoom)
        {
            distance -= Input.mouseScrollDelta.y * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void ResetView()
    {
        yaw = defaultYaw;
        pitch = defaultPitch;
        distance = defaultDistance;
        followVelocity = Vector3.zero;
    }

    private Vector3 GetOrbitOffset()
    {
        float yawRadians = yaw * Mathf.Deg2Rad;
        float pitchRadians = pitch * Mathf.Deg2Rad;
        float horizontalDistance = Mathf.Cos(pitchRadians) * distance;

        return new Vector3(
            Mathf.Sin(yawRadians) * horizontalDistance,
            Mathf.Sin(pitchRadians) * distance,
            Mathf.Cos(yawRadians) * horizontalDistance);
    }

    private void FindPlayerIfNeeded()
    {
        if (!autoFindPlayer || target != null)
        {
            return;
        }

        CatPlayerController player = PreferredPlayerFinder.FindPreferredPlayer();
        if (player != null)
        {
            target = player.transform;
        }
    }
}
