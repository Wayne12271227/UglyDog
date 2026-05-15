using Fusion;
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

    [Header("Combat")]
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackRange = 1.6f;
    [SerializeField] private float attackRadius = 0.75f;
    [SerializeField] private float attackForwardOffset = 0.75f;
    [SerializeField] private float attackKnockbackDistance = 4.2f;
    [SerializeField] private float attackCooldown = 5f;
    [SerializeField] private float attackAnimationSpeed = 2.5f;
    [SerializeField] private float attackAnimationSpeedDuration = 0.4f;

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
    private NetworkObject networkObject;
    private UglyDogNetworkPlayer networkPlayer;
    private PlayerCombatant playerCombatant;
    private bool networkControlled;
    private float currentInputMagnitude;
    private UglyDogNetworkAction lastRequestedNetworkAction;
    private float nextAttackTime;
    private float restoreAnimatorSpeedTime;
    private float defaultAnimatorSpeed = 1f;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            defaultAnimatorSpeed = animator.speed;
        }

        capsuleCollider = GetComponent<CapsuleCollider>();
        playerRigidbody = GetComponent<Rigidbody>();
        networkObject = GetComponent<NetworkObject>();
        networkPlayer = GetComponent<UglyDogNetworkPlayer>();
        playerCombatant = GetComponent<PlayerCombatant>();
        if (playerCombatant == null)
        {
            playerCombatant = gameObject.AddComponent<PlayerCombatant>();
        }

        ConfigureRigidbodyForTopDown();
        CacheAnimatorParameters();
    }

    private void Update()
    {
        if (networkControlled)
        {
            return;
        }

        if (PreferredPlayerFinder.FindPreferredPlayer() != this)
        {
            UpdateAnimation(0f);
            return;
        }

        if (UpgradeShopUI.BlocksPlayerInput)
        {
            UpdateAnimation(0f);
            return;
        }

        Vector3 input = GetMovementInput();
        bool attackPressed = Input.GetKeyDown(attackKey);
        ApplyMovementInput(input, attackPressed, Time.deltaTime);
    }

    public bool HasLocalPlayerAuthority()
    {
        return networkObject == null || networkObject.Runner == null || networkObject.HasInputAuthority;
    }

    public bool HasRunningNetworkInputAuthority()
    {
        return networkObject != null && networkObject.Runner != null && networkObject.HasInputAuthority;
    }

    public void SetNetworkControlled(bool isNetworkControlled)
    {
        networkControlled = isNetworkControlled;
        if (!networkControlled)
        {
            currentInputMagnitude = 0f;
        }
    }

    public void ApplyNetworkInput(Vector2 moveInput, bool attackPressed, float deltaTime)
    {
        ApplyMovementInput(new Vector3(moveInput.x, 0f, moveInput.y), attackPressed, deltaTime);
    }

    public void ApplyNetworkAnimation(float speedValue)
    {
        currentInputMagnitude = Mathf.Clamp01(speedValue);
        UpdateAnimation(currentInputMagnitude);
    }

    private void ApplyMovementInput(Vector3 input, bool attackPressed, float deltaTime)
    {
        input = Vector3.ClampMagnitude(input, 1f);
        currentInputMagnitude = input.magnitude;

        Vector3 moveDirection = GetMoveDirection(input);
        bool isWalking = moveDirection.sqrMagnitude > 0.001f;

        if (isWalking)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up) * Quaternion.Euler(0f, modelForwardOffsetY, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * deltaTime);
        }

        MovePlayer(moveDirection * GetEffectiveMoveSpeed() * deltaTime);
        SnapToGroundIfNeeded();

        UpdateAnimation(input.magnitude);

        if (attackPressed)
        {
            TryKickAttack();
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

    private float GetEffectiveMoveSpeed()
    {
        PlayerUpgradeManager upgrades = PlayerUpgradeManager.Instance;
        float multiplier = upgrades != null ? upgrades.MoveSpeedMultiplier : 1f;
        return moveSpeed * multiplier;
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

        if (restoreAnimatorSpeedTime > 0f && Time.time >= restoreAnimatorSpeedTime)
        {
            animator.speed = defaultAnimatorSpeed;
            restoreAnimatorSpeedTime = 0f;
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
        SpeedUpAttackAnimation();
        PlayAction(attackHash, hasAttackTrigger, attackStateName);
    }

    public void ApplyKnockback(Vector3 direction, float distance)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f || distance <= 0f)
        {
            return;
        }

        MovePlayer(direction.normalized * distance);
        SnapToGroundIfNeeded();
    }

    public void PlayDig()
    {
        RequestNetworkAction(UglyDogNetworkAction.Dig);
        sustainedActionStateName = digStateName;
        PlayAction(digHash, hasDigTrigger, digStateName);
    }

    public void PlayBuild()
    {
        RequestNetworkAction(UglyDogNetworkAction.Build);
        sustainedActionStateName = buildStateName;
        PlayAction(buildHash, hasBuildTrigger, buildStateName);
    }

    public bool HasMovementInput(float threshold = 0.05f)
    {
        return GetCurrentInputMagnitude() > threshold;
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
        RequestNetworkAction(UglyDogNetworkAction.Stop);

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
        if (networkControlled)
        {
            return currentInputMagnitude;
        }

        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        return Vector3.ClampMagnitude(input, 1f).magnitude;
    }

    private void RequestNetworkAction(UglyDogNetworkAction action)
    {
        if (!networkControlled || networkPlayer == null || !HasRunningNetworkInputAuthority())
        {
            return;
        }

        if (lastRequestedNetworkAction == action)
        {
            return;
        }

        lastRequestedNetworkAction = action;
        networkPlayer.RequestAction(action);
    }

    private void TryKickAttack()
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackCooldown;
        PlayAttack();
        PerformKickAttack();
    }

    private void PerformKickAttack()
    {

        MinionTeam attackerTeam = PreferredPlayerFinder.IsPlayerTeam(this, MinionTeam.Cat) ? MinionTeam.Cat : MinionTeam.Dog;
        Vector3 forward = GetAttackForward();
        Vector3 center = transform.position + forward * attackForwardOffset + Vector3.up * 0.8f;
        Collider[] hits = Physics.OverlapSphere(center, attackRadius + attackRange * 0.5f, ~0, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            Vector3 toTarget = hit.bounds.center - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > attackRange * attackRange)
            {
                continue;
            }

            if (Vector3.Dot(forward, toTarget.normalized) < 0.2f)
            {
                continue;
            }

            if (TryKickMinion(hit, attackerTeam, forward) || TryKickPlayer(hit, attackerTeam, forward))
            {
                return;
            }
        }
    }

    private void SpeedUpAttackAnimation()
    {
        if (animator == null)
        {
            return;
        }

        animator.speed = attackAnimationSpeed;
        restoreAnimatorSpeedTime = Time.time + attackAnimationSpeedDuration;
    }

    private bool TryKickMinion(Collider hit, MinionTeam attackerTeam, Vector3 forward)
    {
        MinionCombatant minion = hit.GetComponentInParent<MinionCombatant>();
        if (minion == null || minion.Team == attackerTeam || minion.IsDead)
        {
            return false;
        }

        minion.TakeDamage(attackDamage);
        MinionUnit unit = minion.GetComponent<MinionUnit>();
        if (unit != null)
        {
            unit.ApplyKnockback(forward, attackKnockbackDistance);
        }
        else
        {
            minion.transform.position += forward.normalized * attackKnockbackDistance;
        }

        return true;
    }

    private bool TryKickPlayer(Collider hit, MinionTeam attackerTeam, Vector3 forward)
    {
        CatPlayerController targetPlayer = hit.GetComponentInParent<CatPlayerController>();
        if (targetPlayer == null || targetPlayer == this || !PreferredPlayerFinder.IsPlayerTeam(targetPlayer, GetEnemyTeam(attackerTeam)))
        {
            return false;
        }

        PlayerCombatant targetCombatant = targetPlayer.GetComponent<PlayerCombatant>();
        if (targetCombatant == null)
        {
            targetCombatant = targetPlayer.gameObject.AddComponent<PlayerCombatant>();
        }

        targetCombatant.TakeDamage(attackDamage);
        targetPlayer.ApplyKnockback(forward, attackKnockbackDistance);
        return true;
    }

    private Vector3 GetAttackForward()
    {
        Vector3 forward = (transform.rotation * Quaternion.Euler(0f, -modelForwardOffsetY, 0f)) * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.001f)
        {
            forward = Vector3.forward;
        }

        return forward.normalized;
    }

    private static MinionTeam GetEnemyTeam(MinionTeam team)
    {
        return team == MinionTeam.Dog ? MinionTeam.Cat : MinionTeam.Dog;
    }

    private void ConfigureRigidbodyForTopDown()
    {
        if (playerRigidbody == null)
        {
            return;
        }

        playerRigidbody.useGravity = false;
        playerRigidbody.constraints |= RigidbodyConstraints.FreezeRotation;
        playerRigidbody.constraints |= RigidbodyConstraints.FreezePositionY;
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
