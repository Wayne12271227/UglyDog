using System.Collections;
using Fusion;
using UnityEngine;

public class CatPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.3f;
    [SerializeField] private float turnSpeed = 12f;
    [SerializeField] private bool moveRelativeToCamera = true;
    [SerializeField] private float modelForwardOffsetY = -90f;
    [SerializeField] private LayerMask collisionLayers = ~0;
    [SerializeField] private float collisionSkin = 0.03f;
    [SerializeField, Range(0f, 1f)] private float walkableCollisionNormalY = 0.25f;
    [SerializeField] private bool resolveCollisionPenetration = true;
    [SerializeField] private int penetrationResolveIterations = 1;
    [SerializeField] private float penetrationSkin = 0.005f;
    [SerializeField] private float maxPenetrationCorrection = 0.06f;

    [Header("Grounding")]
    [SerializeField] private bool snapToGround = true;
    [SerializeField] private bool autoAlignCapsuleToGround = true;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float groundProbeHeight = 3f;
    [SerializeField] private float maxGroundSnapDistance = 6f;
    [SerializeField] private float groundSkin = 0.01f;
    [SerializeField] private float maxGroundStepUp = 1.2f;

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
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackRange = 1.6f;
    [SerializeField] private float attackRadius = 0.75f;
    [SerializeField] private float attackForwardOffset = 0.75f;
    [SerializeField] private float attackKnockbackDistance = 4.2f;
    [SerializeField] private float attackCooldown = 5f;
    [SerializeField] private float attackAnimationSpeed = 2.5f;
    [SerializeField] private float attackAnimationSpeedDuration = 0.4f;

    [Header("Audio")]
    [SerializeField] private AudioSource actionAudioSource;
    [SerializeField] private AudioClip woodDigActionClip;
    [SerializeField] private AudioClip stoneDigActionClip;
    [SerializeField] private AudioClip footstepClip;
    [SerializeField] private AudioClip attackClip;
    [SerializeField] private AudioClip attackHitClip;
    [SerializeField] private AudioClip buildClip;
    [SerializeField] private AudioClip coinGainClip;
    [SerializeField, Range(0f, 1f)] private float digActionVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.75f;
    [SerializeField, Range(0f, 1f)] private float attackVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float attackHitVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float buildVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float coinGainVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float digActionImpactNormalizedTime = 0.55f;
    [SerializeField] private float digActionMinimumInterval = 0.12f;
    [SerializeField] private float footstepInterval = 0.35f;
    [SerializeField] private float buildSoundInterval = 0.55f;

    [Header("Attack Hit Feedback")]
    [SerializeField] private bool enableAttackHitFeedback = true;
    [SerializeField] private float attackHitStopDuration = 0.045f;
    [SerializeField] private float attackHitStopTimeScale = 0.08f;
    [SerializeField] private float attackHitShakeDuration = 0.12f;
    [SerializeField] private float attackHitShakeStrength = 0.18f;
    [SerializeField] private Vector3 attackHitPopupOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Smart Dig Speed")]
    [SerializeField] private bool syncDigSpeedToGatherInterval = true;
    [SerializeField] private float baseDigCycleDuration = 0.5f;
    [SerializeField] private float minSyncedDigAnimatorSpeed = 0.5f;
    [SerializeField] private float maxSyncedDigAnimatorSpeed = 3f;

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
    private float nextDigActionSoundTime;
    private float nextFootstepTime;
    private float nextBuildSoundTime;
    private float buildSoundEndTime;
    private Coroutine hitStopCoroutine;
    private bool hitStopActive;
    private float hitStopOriginalTimeScale = 1f;
    private float hitStopOriginalFixedDeltaTime;
    private ResourceType currentDigResourceType;
    private bool hasCurrentDigResourceType;
    private float currentDigCycleDuration;
    private bool digImpactSoundArmed = true;

    private void Awake()
    {
        OnValidate();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            defaultAnimatorSpeed = animator.speed;
        }

        if (actionAudioSource == null)
        {
            actionAudioSource = GetComponent<AudioSource>();
        }

        if (actionAudioSource == null)
        {
            actionAudioSource = gameObject.AddComponent<AudioSource>();
        }

        actionAudioSource.playOnAwake = false;

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
        ConfigureCapsuleForTopDown();
        CacheAnimatorParameters();
    }

    private void OnDisable()
    {
        if (hitStopCoroutine != null)
        {
            StopCoroutine(hitStopCoroutine);
            hitStopCoroutine = null;
        }

        RestoreHitStopTime();
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

        if (UpgradeShopUI.BlocksPlayerInput || BuildShopUI.BlocksPlayerInput || SettingsPanelUI.BlocksPlayerInput)
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

    public float AttackCooldownRemaining => Mathf.Max(0f, nextAttackTime - Time.time);

    public float AttackCooldownReadyFraction
    {
        get
        {
            if (attackCooldown <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.Clamp01(AttackCooldownRemaining / attackCooldown);
        }
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
        ApplyNetworkInput(moveInput, attackPressed, deltaTime, true, true);
    }

    public void ApplyNetworkInput(Vector2 moveInput, bool attackPressed, float deltaTime, bool allowLocalSideEffects, bool allowGameplayEffects)
    {
        ApplyWorldMovementInput(new Vector3(moveInput.x, 0f, moveInput.y), attackPressed, deltaTime, allowLocalSideEffects, allowGameplayEffects);
    }

    private void ApplyWorldMovementInput(Vector3 worldMoveInput, bool attackPressed, float deltaTime, bool allowLocalSideEffects, bool allowGameplayEffects)
    {
        worldMoveInput = Vector3.ClampMagnitude(worldMoveInput, 1f);
        currentInputMagnitude = worldMoveInput.magnitude;

        bool isWalking = worldMoveInput.sqrMagnitude > 0.001f;
        if (isWalking)
        {
            Quaternion targetRotation = Quaternion.LookRotation(worldMoveInput, Vector3.up) * Quaternion.Euler(0f, modelForwardOffsetY, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * deltaTime);
        }

        MovePlayer(worldMoveInput * GetEffectiveMoveSpeed() * deltaTime);
        SnapToGroundIfNeeded();
        if (allowLocalSideEffects)
        {
            UpdateFootstepAudio(isWalking);
        }

        UpdateAnimation(currentInputMagnitude);

        if (attackPressed)
        {
            TryKickAttack(allowGameplayEffects, allowLocalSideEffects || allowGameplayEffects);
        }
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
        UpdateFootstepAudio(isWalking);

        UpdateAnimation(input.magnitude);

        if (attackPressed)
        {
            TryKickAttack(true, true);
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
        Vector3 horizontalOffset = new Vector3(offset.x, 0f, offset.z);
        if (horizontalOffset.sqrMagnitude > 0.000001f)
        {
            horizontalOffset = GetBlockedHorizontalOffset(horizontalOffset);
        }

        offset = horizontalOffset + Vector3.up * offset.y;
        transform.position += offset;
        ResolveCollisionPenetration();
        if (playerRigidbody != null && playerRigidbody.isKinematic)
        {
            playerRigidbody.position = transform.position;
        }
    }

    private Vector3 GetBlockedHorizontalOffset(Vector3 horizontalOffset)
    {
        if (capsuleCollider == null)
        {
            return horizontalOffset;
        }

        Vector3 direction = horizontalOffset.normalized;
        float distance = horizontalOffset.magnitude;
        GetCapsuleWorldPoints(out Vector3 point1, out Vector3 point2, out float radius);

        RaycastHit[] hits = Physics.CapsuleCastAll(point1, point2, radius, direction, distance + collisionSkin, collisionLayers, QueryTriggerInteraction.Ignore);
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (IsPredictionUnstableCollider(hitCollider))
            {
                continue;
            }

            if (IsEscapingBuildingCollider(hitCollider, horizontalOffset))
            {
                continue;
            }

            if (ShouldIgnoreHorizontalCollision(hitCollider, hits[i].normal))
            {
                continue;
            }

            nearestDistance = Mathf.Min(nearestDistance, hits[i].distance);
        }

        if (float.IsPositiveInfinity(nearestDistance))
        {
            return horizontalOffset;
        }

        float allowedDistance = Mathf.Max(0f, nearestDistance - collisionSkin);
        return direction * Mathf.Min(distance, allowedDistance);
    }

    private void ResolveCollisionPenetration()
    {
        if (!resolveCollisionPenetration || capsuleCollider == null)
        {
            return;
        }

        int iterations = Mathf.Max(1, penetrationResolveIterations);
        for (int i = 0; i < iterations; i++)
        {
            if (!TryResolveSinglePenetration())
            {
                return;
            }
        }
    }

    private bool TryResolveSinglePenetration()
    {
        GetCapsuleWorldPoints(out Vector3 point1, out Vector3 point2, out float radius);
        Collider[] overlaps = Physics.OverlapCapsule(point1, point2, radius + penetrationSkin, collisionLayers, QueryTriggerInteraction.Ignore);
        Vector3 bestDirection = Vector3.zero;
        float bestDistance = 0f;

        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider other = overlaps[i];
            if (other == null || other.transform.IsChildOf(transform))
            {
                continue;
            }

            if (IsPredictionUnstableCollider(other))
            {
                continue;
            }

            if (!Physics.ComputePenetration(
                    capsuleCollider,
                    transform.position,
                    transform.rotation,
                    other,
                    other.transform.position,
                    other.transform.rotation,
                    out Vector3 direction,
                    out float distance))
            {
                continue;
            }

            if (distance <= 0f || direction.y > walkableCollisionNormalY || IsGroundLikeCollider(other) || IsBuildingCollider(other))
            {
                continue;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            if (distance > bestDistance)
            {
                bestDistance = distance;
                bestDirection = direction.normalized;
            }
        }

        if (bestDistance <= 0f || bestDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        float correctionDistance = Mathf.Min(bestDistance, maxPenetrationCorrection);
        transform.position += bestDirection * correctionDistance;
        return true;
    }

    private bool ShouldIgnoreHorizontalCollision(Collider other, Vector3 normal)
    {
        if (IsPredictionUnstableCollider(other))
        {
            return true;
        }

        if (IsGroundLikeCollider(other))
        {
            return true;
        }

        if (normal.y > walkableCollisionNormalY)
        {
            return true;
        }

        return false;
    }

    private bool IsPredictionUnstableCollider(Collider other)
    {
        if (!networkControlled || other == null || IsBuildingCollider(other))
        {
            return false;
        }

        return other.GetComponentInParent<CatPlayerController>() != null
            || other.GetComponentInParent<MinionUnit>() != null
            || other.GetComponentInParent<MinionCombatant>() != null;
    }

    private bool IsEscapingBuildingCollider(Collider other, Vector3 horizontalOffset)
    {
        if (!IsBuildingCollider(other) || horizontalOffset.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        bool isOverlapping = Physics.ComputePenetration(
                capsuleCollider,
                transform.position,
                transform.rotation,
                other,
                other.transform.position,
                other.transform.rotation,
                out _,
                out float separationDistance)
            && separationDistance > 0f;
        if (!isOverlapping)
        {
            return false;
        }

        Vector3 outward = transform.position - other.bounds.center;
        outward.y = 0f;
        if (outward.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        return Vector3.Dot(horizontalOffset.normalized, outward.normalized) > 0.05f;
    }

    private static bool IsBuildingCollider(Collider other)
    {
        return other != null
            && (other.GetComponentInParent<TeamBuilding>() != null
                || other.GetComponentInParent<BuildingHealth>() != null);
    }

    private static bool IsGroundLikeCollider(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (IsBuildingCollider(other))
        {
            return false;
        }

        string layerName = LayerMask.LayerToName(other.gameObject.layer).ToLowerInvariant();
        if (ContainsGroundKeyword(layerName))
        {
            return true;
        }

        Transform current = other.transform;
        while (current != null)
        {
            if (ContainsGroundKeyword(current.name.ToLowerInvariant()))
            {
                return true;
            }

            current = current.parent;
        }

        Bounds bounds = other.bounds;
        return bounds.size.x > 15f && bounds.size.z > 15f && bounds.size.y < 4f;
    }

    private static bool ContainsGroundKeyword(string value)
    {
        return value.Contains("ground")
            || value.Contains("floor")
            || value.Contains("grass")
            || value.Contains("terrain");
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
        UpdateSyncedDigAnimatorSpeed();
        UpdateDigImpactAudio();
    }

    public void PlayAttack()
    {
        sustainedActionStateName = null;
        SpeedUpAttackAnimation();
        PlayOneShot(attackClip, attackVolume);
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

    public void TeleportToGroundedPosition(Vector3 position)
    {
        transform.position = position;
        ResolveCollisionPenetration();
        if (playerRigidbody != null)
        {
            playerRigidbody.position = transform.position;
        }

        SnapToGroundIfNeeded();
        ResolveCollisionPenetration();
        if (playerRigidbody != null)
        {
            playerRigidbody.position = transform.position;
        }
    }

    public void PlayDig()
    {
        RequestNetworkAction(UglyDogNetworkAction.Dig);
        sustainedActionStateName = digStateName;
        PlayAction(digHash, hasDigTrigger, digStateName);
    }

    public void PlayDig(ResourceType resourceType)
    {
        currentDigResourceType = resourceType;
        hasCurrentDigResourceType = true;

        if (digActionImpactNormalizedTime <= 0f)
        {
            PlayDigActionSound(resourceType);
        }

        PlayDig();
    }

    public void PlayDig(ResourceType resourceType, float digCycleDuration)
    {
        currentDigCycleDuration = digCycleDuration;
        PlayDig(resourceType);
    }

    public void PlayBuild()
    {
        RequestNetworkAction(UglyDogNetworkAction.Build);
        sustainedActionStateName = buildStateName;
        PlayBuildSoundIfReady();
        PlayAction(buildHash, hasBuildTrigger, buildStateName);
    }

    public void PlayCoinGainSound()
    {
        PlayOneShot(coinGainClip, coinGainVolume);
    }

    public bool HasMovementInput(float threshold = 0.05f)
    {
        return GetCurrentInputMagnitude() > threshold;
    }

    public void StopAction()
    {
        StopBuildSound();

        if (animator == null)
        {
            return;
        }

        ResetTriggerIfAvailable(attackHash, hasAttackTrigger);
        ResetTriggerIfAvailable(digHash, hasDigTrigger);
        ResetTriggerIfAvailable(buildHash, hasBuildTrigger);
        sustainedActionStateName = null;
        hasCurrentDigResourceType = false;
        currentDigCycleDuration = 0f;
        digImpactSoundArmed = true;
        if (restoreAnimatorSpeedTime <= 0f)
        {
            animator.speed = defaultAnimatorSpeed;
        }
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

    private void UpdateFootstepAudio(bool isWalking)
    {
        if (!isWalking)
        {
            nextFootstepTime = 0f;
            return;
        }

        if (Time.time < nextFootstepTime)
        {
            return;
        }

        nextFootstepTime = Time.time + footstepInterval;
        PlayOneShot(footstepClip, footstepVolume);
    }

    private void PlayBuildSoundIfReady()
    {
        if (Time.time < nextBuildSoundTime)
        {
            return;
        }

        float buildSoundCooldown = buildSoundInterval;
        if (buildClip != null)
        {
            buildSoundCooldown = Mathf.Max(buildSoundCooldown, buildClip.length);
            buildSoundEndTime = Time.time + buildClip.length;
        }
        else
        {
            buildSoundEndTime = Time.time;
        }

        nextBuildSoundTime = Time.time + buildSoundCooldown;
        PlayOneShot(buildClip, buildVolume);
    }

    private void StopBuildSound()
    {
        if (actionAudioSource == null || Time.time >= buildSoundEndTime)
        {
            return;
        }

        actionAudioSource.Stop();
        buildSoundEndTime = 0f;
        nextBuildSoundTime = 0f;
    }

    private void UpdateDigImpactAudio()
    {
        if (animator == null || !hasCurrentDigResourceType || string.IsNullOrEmpty(digStateName))
        {
            digImpactSoundArmed = true;
            return;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (!currentState.IsName(digStateName))
        {
            digImpactSoundArmed = true;
            return;
        }

        float normalizedTime = currentState.normalizedTime % 1f;
        if (digActionImpactNormalizedTime <= 0f)
        {
            digImpactSoundArmed = false;
            return;
        }

        if (normalizedTime < digActionImpactNormalizedTime)
        {
            digImpactSoundArmed = true;
            return;
        }

        if (!digImpactSoundArmed || Time.time < nextDigActionSoundTime)
        {
            return;
        }

        digImpactSoundArmed = false;
        PlayDigActionSound(currentDigResourceType);
    }

    private void UpdateSyncedDigAnimatorSpeed()
    {
        if (!syncDigSpeedToGatherInterval || animator == null || !hasCurrentDigResourceType || currentDigCycleDuration <= 0f)
        {
            return;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (!currentState.IsName(digStateName))
        {
            return;
        }

        float speedMultiplier = baseDigCycleDuration / currentDigCycleDuration;
        speedMultiplier = Mathf.Clamp(speedMultiplier, minSyncedDigAnimatorSpeed, maxSyncedDigAnimatorSpeed);
        animator.speed = defaultAnimatorSpeed * speedMultiplier;
    }

    private void PlayDigActionSound(ResourceType resourceType)
    {
        if (Time.time < nextDigActionSoundTime)
        {
            return;
        }

        float smartMinimumInterval = digActionMinimumInterval;
        if (currentDigCycleDuration > 0f)
        {
            smartMinimumInterval = Mathf.Min(smartMinimumInterval, Mathf.Max(0.01f, currentDigCycleDuration * 0.25f));
        }

        nextDigActionSoundTime = Time.time + smartMinimumInterval;
        switch (resourceType)
        {
            case ResourceType.Wood:
                PlayOneShot(woodDigActionClip, digActionVolume);
                break;
            case ResourceType.Stone:
                PlayOneShot(stoneDigActionClip, digActionVolume);
                break;
        }
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null || actionAudioSource == null || volume <= 0f)
        {
            return;
        }

        actionAudioSource.PlayOneShot(clip, volume * GameAudioSettings.SfxVolume);
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

    private void TryKickAttack(bool applyGameplayEffects, bool playLocalFeedback)
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackCooldown;
        if (playLocalFeedback)
        {
            PlayAttack();
        }

        if (applyGameplayEffects)
        {
            PerformKickAttack(playLocalFeedback);
        }
    }

    private void PerformKickAttack(bool playLocalFeedback)
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

            if (TryKickMinion(hit, attackerTeam, forward, playLocalFeedback)
                || TryKickBuilding(hit, attackerTeam, playLocalFeedback)
                || TryKickPlayer(hit, attackerTeam, forward, playLocalFeedback))
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

    private bool TryKickMinion(Collider hit, MinionTeam attackerTeam, Vector3 forward, bool playLocalFeedback)
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

        PlayAttackHitFeedback(minion.transform.position + attackHitPopupOffset, playLocalFeedback);
        return true;
    }

    private bool TryKickPlayer(Collider hit, MinionTeam attackerTeam, Vector3 forward, bool playLocalFeedback)
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
        PlayAttackHitFeedback(targetPlayer.transform.position + attackHitPopupOffset, playLocalFeedback);
        return true;
    }

    private bool TryKickBuilding(Collider hit, MinionTeam attackerTeam, bool playLocalFeedback)
    {
        TeamBuilding building = hit.GetComponentInParent<TeamBuilding>();
        if (building == null || building.Team == attackerTeam || building.Health == null || building.Health.IsDestroyed)
        {
            return false;
        }

        building.Health.TakeDamage(attackDamage);
        PlayAttackHitFeedback(hit.bounds.center + attackHitPopupOffset, playLocalFeedback);
        return true;
    }

    private void PlayAttackHitFeedback(Vector3 hitPosition, bool playLocalFeedback)
    {
        if (!enableAttackHitFeedback || !playLocalFeedback)
        {
            return;
        }

        AudioClip hitClip = attackHitClip != null ? attackHitClip : attackClip;
        PlayOneShot(hitClip, attackHitVolume);
        CameraHitShake.ShakeMainCamera(attackHitShakeDuration, attackHitShakeStrength);
        DamagePopup.Spawn(hitPosition, "-" + attackDamage);

        if (attackHitStopDuration > 0f && attackHitStopTimeScale > 0f && attackHitStopTimeScale < 1f)
        {
            if (hitStopCoroutine != null)
            {
                StopCoroutine(hitStopCoroutine);
                RestoreHitStopTime();
            }

            hitStopCoroutine = StartCoroutine(PlayHitStop());
        }
    }

    private IEnumerator PlayHitStop()
    {
        hitStopActive = true;
        hitStopOriginalTimeScale = Time.timeScale;
        hitStopOriginalFixedDeltaTime = Time.fixedDeltaTime;

        Time.timeScale = Mathf.Clamp(attackHitStopTimeScale, 0.01f, 1f);
        Time.fixedDeltaTime = hitStopOriginalFixedDeltaTime * Time.timeScale;

        yield return new WaitForSecondsRealtime(attackHitStopDuration);

        RestoreHitStopTime();
        hitStopCoroutine = null;
    }

    private void RestoreHitStopTime()
    {
        if (!hitStopActive)
        {
            return;
        }

        Time.timeScale = hitStopOriginalTimeScale;
        Time.fixedDeltaTime = hitStopOriginalFixedDeltaTime;
        hitStopActive = false;
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
        playerRigidbody.isKinematic = true;
        playerRigidbody.constraints |= RigidbodyConstraints.FreezeRotation;
        playerRigidbody.constraints |= RigidbodyConstraints.FreezePositionY;
    }

    private void ConfigureCapsuleForTopDown()
    {
        if (!autoAlignCapsuleToGround || capsuleCollider == null)
        {
            return;
        }

        Vector3 center = capsuleCollider.center;
        float expectedCenterY = capsuleCollider.height * 0.5f;
        if (center.y < expectedCenterY * 0.5f)
        {
            center.y = expectedCenterY;
            capsuleCollider.center = center;
        }
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

            if (IsBuildingCollider(hitCollider))
            {
                continue;
            }

            if (!IsGroundLikeCollider(hitCollider))
            {
                continue;
            }

            float stepUp = hits[i].point.y + groundSkin - GetCapsuleBottomWorldY();
            if (stepUp > maxGroundStepUp)
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

    private void GetCapsuleWorldPoints(out Vector3 point1, out Vector3 point2, out float radius)
    {
        Vector3 worldCenter = transform.TransformPoint(capsuleCollider.center);
        float scaleX = Mathf.Abs(transform.lossyScale.x);
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        float scaleZ = Mathf.Abs(transform.lossyScale.z);
        radius = capsuleCollider.radius * Mathf.Max(scaleX, scaleZ);
        float height = Mathf.Max(capsuleCollider.height * scaleY, radius * 2f);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        point1 = worldCenter + Vector3.up * halfSegment;
        point2 = worldCenter - Vector3.up * halfSegment;
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

    private void OnValidate()
    {
        digActionMinimumInterval = Mathf.Max(0.01f, digActionMinimumInterval);
        footstepInterval = Mathf.Max(0.05f, footstepInterval);
        buildSoundInterval = Mathf.Max(0.05f, buildSoundInterval);
        collisionSkin = Mathf.Max(0.001f, collisionSkin);
        walkableCollisionNormalY = Mathf.Clamp01(walkableCollisionNormalY);
        penetrationResolveIterations = Mathf.Clamp(penetrationResolveIterations, 1, 2);
        penetrationSkin = Mathf.Clamp(penetrationSkin, 0f, 0.02f);
        maxPenetrationCorrection = Mathf.Clamp(maxPenetrationCorrection, 0.01f, 0.1f);
        maxGroundStepUp = Mathf.Clamp(maxGroundStepUp, 0.8f, 2f);
        baseDigCycleDuration = Mathf.Max(0.05f, baseDigCycleDuration);
        minSyncedDigAnimatorSpeed = Mathf.Max(0.05f, minSyncedDigAnimatorSpeed);
        maxSyncedDigAnimatorSpeed = Mathf.Max(minSyncedDigAnimatorSpeed, maxSyncedDigAnimatorSpeed);
    }
}
