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

    private Camera cameraComponent;
    private Vector3 followVelocity;

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        ApplyCameraSettings();
        FindPlayerIfNeeded();
        SnapToTarget();
    }

    private void OnValidate()
    {
        cameraComponent = GetComponent<Camera>();
        ApplyCameraSettings();
        SnapToTarget();
    }

    private void LateUpdate()
    {
        FindPlayerIfNeeded();

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
        return target.position + offset;
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

    private void FindPlayerIfNeeded()
    {
        if (!autoFindPlayer || target != null)
        {
            return;
        }

        CatPlayerController player = FindObjectOfType<CatPlayerController>();
        if (player != null)
        {
            target = player.transform;
        }
    }
}
