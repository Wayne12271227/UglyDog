using UnityEngine;

public class CatPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float turnSpeed = 12f;
    [SerializeField] private bool moveRelativeToCamera = true;
    [SerializeField] private float modelForwardOffsetY = -90f;

    [Header("Grounding")]
    [SerializeField] private bool snapToGround = true;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float groundProbeHeight = 3f;
    [SerializeField] private float maxGroundSnapDistance = 6f;
    [SerializeField] private float groundSkin = 0.01f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private KeyCode attackKey = KeyCode.J;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string digTrigger = "Dig";
    [SerializeField] private string buildTrigger = "Build";
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string runStateName = "Run";
    [SerializeField] private string attackStateName = "Attack";
    [SerializeField] private string digStateName = "Dig";
    [SerializeField] private string buildStateName = "Build";
    [SerializeField] private float actionStartBlendTime = 0.05f;
    [SerializeField] private float actionStopBlendTime = 0.02f;
    [SerializeField] private float nonLoopingRunRestartTime = 0.9f;
    [SerializeField] private float runLoopBlendTime = 0.08f;
    [SerializeField] private float actionLoopRestartTime = 0.92f;
    [SerializeField] private float actionLoopBlendTime = 0.03f;

    private int speedHash;
    private int attackHash;
    private int digHash;
    private int buildHash;
    private bool hasSpeedParameter;
    private bool hasAttackTrigger;
    private bool hasDigTrigger;
    private bool hasBuildTrigger;
    private float currentSpeedValue;
    private string sustainedActionStateName;
    private CapsuleCollider capsuleCollider;
    private Rigidbody playerRigidbody;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        capsuleCollider = GetComponent<CapsuleCollider>();
        playerRigidbody = GetComponent<Rigidbody>();
        CacheAnimatorParameters();
    }

    private void Update()
    {
        Vector3 input = GetMovementInput();
        input = Vector3.ClampMagnitude(input, 1f);

        Vector3 moveDirection = GetMoveDirection(input);
        bool isWalking = moveDirection.sqrMagnitude > 0.001f;

        if (isWalking)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up) * Quaternion.Euler(0f, modelForwardOffsetY, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        MovePlayer(moveDirection * moveSpeed * Time.deltaTime);
        SnapToGroundIfNeeded();

        UpdateAnimation(input.magnitude);

        if (Input.GetKeyDown(attackKey))
        {
            PlayAttack();
        }
    }

    private Vector3 GetMoveDirection(Vector3 input)
    {
        if (input.sqrMagnitude <= 0.001f)
        {
            return Vector3.zero;
        }

        if (!moveRelativeToCamera || Camera.main == null)
        {
            return input.normalized;
        }

        Vector3 cameraForward = Camera.main.transform.forward;
        Vector3 cameraRight = Camera.main.transform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;

        return (cameraForward.normalized * input.z + cameraRight.normalized * input.x).normalized;
    }

    private Vector3 GetMovementInput()
    {
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 0.001f)
        {
            return input;
        }

        float horizontal = 0f;
        float vertical = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            horizontal -= 1f;
        }

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            horizontal += 1f;
        }

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            vertical -= 1f;
        }

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            vertical += 1f;
        }

        return new Vector3(horizontal, 0f, vertical);
    }

    private void MovePlayer(Vector3 offset)
    {
        transform.position += offset;
        if (playerRigidbody != null && playerRigidbody.isKinematic)
        {
            playerRigidbody.position = transform.position;
        }
    }

    private void UpdateAnimation(float speedValue)
    {
        currentSpeedValue = speedValue;

        if (animator == null)
        {
            return;
        }

        if (hasSpeedParameter)
        {
            animator.SetFloat(speedHash, speedValue);
        }

        RestartRunStateIfNeeded(speedValue);
        RestartSustainedActionIfNeeded();
    }

    public void PlayAttack()
    {
        sustainedActionStateName = null;
        PlayAction(attackHash, hasAttackTrigger, attackStateName);
    }

    public void PlayDig()
    {
        sustainedActionStateName = digStateName;
        PlayAction(digHash, hasDigTrigger, digStateName);
    }

    public void PlayBuild()
    {
        sustainedActionStateName = buildStateName;
        PlayAction(buildHash, hasBuildTrigger, buildStateName);
    }

    public void StopAction()
    {
        if (animator == null)
        {
            return;
        }

        ResetTriggerIfAvailable(attackHash, hasAttackTrigger);
        ResetTriggerIfAvailable(digHash, hasDigTrigger);
        ResetTriggerIfAvailable(buildHash, hasBuildTrigger);
        sustainedActionStateName = null;

        currentSpeedValue = GetCurrentInputMagnitude();
        if (hasSpeedParameter)
        {
            animator.SetFloat(speedHash, currentSpeedValue);
        }

        string targetState = currentSpeedValue > 0.05f ? runStateName : idleStateName;
        if (!string.IsNullOrEmpty(targetState))
        {
            CrossFadeToState(targetState, actionStopBlendTime, false);
        }
    }

    private void PlayAction(int triggerHash, bool hasTrigger, string stateName)
    {
        if (animator == null || !hasTrigger)
        {
            return;
        }

        if (IsCurrentOrNextState(stateName))
        {
            return;
        }

        if (CrossFadeToState(stateName, actionStartBlendTime, true))
        {
            return;
        }

        animator.SetTrigger(triggerHash);
    }

    private void ResetTriggerIfAvailable(int triggerHash, bool hasTrigger)
    {
        if (animator != null && hasTrigger)
        {
            animator.ResetTrigger(triggerHash);
        }
    }

    private bool IsCurrentOrNextState(string stateName)
    {
        if (string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (currentState.IsName(stateName))
        {
            return true;
        }

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
            return nextState.IsName(stateName);
        }

        return false;
    }

    private bool CrossFadeToState(string stateName, float blendTime, bool requireKnownState)
    {
        int fullPathHash = Animator.StringToHash("Base Layer." + stateName);
        if (animator.HasState(0, fullPathHash))
        {
            animator.CrossFadeInFixedTime(fullPathHash, blendTime, 0);
            return true;
        }

        if (requireKnownState)
        {
            return false;
        }

        animator.CrossFadeInFixedTime(stateName, blendTime, 0);
        return true;
    }

    private void RestartRunStateIfNeeded(float speedValue)
    {
        if (speedValue <= 0.05f || string.IsNullOrEmpty(runStateName) || animator.IsInTransition(0))
        {
            return;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (!currentState.IsName(runStateName) || currentState.loop)
        {
            return;
        }

        if (currentState.normalizedTime >= nonLoopingRunRestartTime)
        {
            CrossFadeToState(runStateName, runLoopBlendTime, true);
        }
    }

    private void RestartSustainedActionIfNeeded()
    {
        if (string.IsNullOrEmpty(sustainedActionStateName) || animator.IsInTransition(0))
        {
            return;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (!currentState.IsName(sustainedActionStateName) || currentState.loop)
        {
            return;
        }

        if (currentState.normalizedTime >= actionLoopRestartTime)
        {
            CrossFadeToState(sustainedActionStateName, actionLoopBlendTime, true);
        }
    }

    private float GetCurrentInputMagnitude()
    {
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        return Vector3.ClampMagnitude(input, 1f).magnitude;
    }

    private void SnapToGroundIfNeeded()
    {
        if (!snapToGround || capsuleCollider == null)
        {
            return;
        }

        Vector3 origin = transform.position + Vector3.up * groundProbeHeight;
        float rayDistance = groundProbeHeight + maxGroundSnapDistance;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, rayDistance, groundLayers, QueryTriggerInteraction.Ignore);

        bool foundGround = false;
        float bestDistance = float.PositiveInfinity;
        Vector3 bestPoint = Vector3.zero;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform) || hits[i].normal.y < 0.5f)
            {
                continue;
            }

            if (hits[i].distance < bestDistance)
            {
                foundGround = true;
                bestDistance = hits[i].distance;
                bestPoint = hits[i].point;
            }
        }

        if (!foundGround)
        {
            return;
        }

        float capsuleBottomY = GetCapsuleBottomWorldY();
        float offsetY = bestPoint.y + groundSkin - capsuleBottomY;
        if (Mathf.Abs(offsetY) > 0.001f)
        {
            MovePlayer(Vector3.up * offsetY);
        }
    }

    private float GetCapsuleBottomWorldY()
    {
        Vector3 worldCenter = transform.TransformPoint(capsuleCollider.center);
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        return worldCenter.y - capsuleCollider.height * scaleY * 0.5f;
    }

    private void CacheAnimatorParameters()
    {
        if (animator == null)
        {
            return;
        }

        speedHash = Animator.StringToHash(speedParameter);
        attackHash = Animator.StringToHash(attackTrigger);
        digHash = Animator.StringToHash(digTrigger);
        buildHash = Animator.StringToHash(buildTrigger);

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == speedParameter && parameter.type == AnimatorControllerParameterType.Float)
            {
                hasSpeedParameter = true;
            }

            if (parameter.name == attackTrigger && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasAttackTrigger = true;
            }

            if (parameter.name == digTrigger && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasDigTrigger = true;
            }

            if (parameter.name == buildTrigger && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasBuildTrigger = true;
            }
        }
    }
}
