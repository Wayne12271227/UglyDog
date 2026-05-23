using UnityEngine;

public class MinionVisualAnimator : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float walkCycleSpeed = 8f;
    [SerializeField] private float walkBobHeight = 0f;
    [SerializeField] private float walkPitchDegrees = 5f;
    [SerializeField] private float walkRollDegrees = 7f;
    [SerializeField] private float attackDuration = 0.28f;
    [SerializeField] private float attackLungeDistance = 0.24f;
    [SerializeField] private float attackPitchDegrees = -12f;
    [SerializeField] private Vector3 attackScalePunch = new Vector3(0.08f, -0.05f, 0.12f);

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation = Quaternion.identity;
    private Vector3 baseLocalScale = Vector3.one;
    private float walkPhase;
    private float moveBlend;
    private float targetMoveBlend;
    private float speedBlend = 1f;
    private float attackTimer;
    private bool initialized;

    public void Initialize(Transform root)
    {
        visualRoot = root;
        CaptureBasePose();
    }

    public void SetBaseLocalPosition(Vector3 localPosition)
    {
        if (visualRoot == null)
        {
            return;
        }

        baseLocalPosition = localPosition;
        visualRoot.localPosition = localPosition;
        initialized = true;
    }

    public void SetMoving(bool moving, float normalizedSpeed = 1f)
    {
        targetMoveBlend = moving ? 1f : 0f;
        speedBlend = Mathf.Clamp01(normalizedSpeed);
    }

    public void PlayAttack()
    {
        attackTimer = attackDuration;
    }

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot = transform.childCount > 0 ? transform.GetChild(0) : null;
        }

        CaptureBasePose();
    }

    private void OnEnable()
    {
        CaptureBasePose();
    }

    private void OnDisable()
    {
        ResetVisualPose();
    }

    private void Update()
    {
        if (visualRoot == null)
        {
            return;
        }

        if (!initialized)
        {
            CaptureBasePose();
        }

        float delta = Time.deltaTime;
        moveBlend = Mathf.MoveTowards(moveBlend, targetMoveBlend, delta * 8f);
        walkPhase += delta * walkCycleSpeed * Mathf.Lerp(0.65f, 1.35f, speedBlend) * Mathf.Max(0.15f, moveBlend);

        float bob = Mathf.Sin(walkPhase) * walkBobHeight * moveBlend;
        float pitch = Mathf.Sin(walkPhase) * walkPitchDegrees * moveBlend;
        float roll = Mathf.Cos(walkPhase * 0.5f) * walkRollDegrees * moveBlend;

        float attackBlend = 0f;
        if (attackTimer > 0f && attackDuration > 0f)
        {
            attackTimer = Mathf.Max(0f, attackTimer - delta);
            float t = 1f - attackTimer / attackDuration;
            attackBlend = Mathf.Sin(t * Mathf.PI);
        }

        Vector3 attackOffset = Vector3.forward * attackLungeDistance * attackBlend;
        Vector3 attackScale = attackScalePunch * attackBlend;
        float attackPitch = attackPitchDegrees * attackBlend;

        visualRoot.localPosition = baseLocalPosition + Vector3.up * bob + attackOffset;
        visualRoot.localRotation = baseLocalRotation * Quaternion.Euler(pitch + attackPitch, 0f, roll);
        visualRoot.localScale = new Vector3(
            baseLocalScale.x * (1f + attackScale.x),
            baseLocalScale.y * (1f + attackScale.y),
            baseLocalScale.z * (1f + attackScale.z));
    }

    private void CaptureBasePose()
    {
        if (visualRoot == null)
        {
            return;
        }

        baseLocalPosition = visualRoot.localPosition;
        baseLocalRotation = visualRoot.localRotation;
        baseLocalScale = visualRoot.localScale;
        initialized = true;
    }

    private void ResetVisualPose()
    {
        if (visualRoot == null || !initialized)
        {
            return;
        }

        visualRoot.localPosition = baseLocalPosition;
        visualRoot.localRotation = baseLocalRotation;
        visualRoot.localScale = baseLocalScale;
    }
}
