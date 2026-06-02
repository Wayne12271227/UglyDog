using UnityEngine;

[RequireComponent(typeof(MinionCombatant))]
public class MinionUnit : MonoBehaviour
{
    [SerializeField] private MinionKind kind = MinionKind.Melee;
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float turnSpeed = 12f;
    [SerializeField] private float targetSearchRadius = 7f;
    [SerializeField] private float buildingSearchRadius = 24f;
    [SerializeField] private float buildingPriorityRadius = 12f;
    [SerializeField] private float minionInterruptRadius = 2.2f;
    [SerializeField] private float attackRange = 1.25f;
    [SerializeField] private int attackDamage = 4;
    [SerializeField] private int buildingAttackDamage = 4;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float goalStopDistance = 1.8f;
    [SerializeField] private float groundProbeHeight = 3f;
    [SerializeField] private float groundSnapDistance = 6f;
    [SerializeField] private float groundSkin = 0.005f;
    [SerializeField] private float playerSeparationRadius = 0.95f;
    [SerializeField] private float playerSeparationStrength = 4f;
    [SerializeField] private LayerMask collisionLayers = ~0;
    [SerializeField] private float collisionSkin = 0.02f;
    [SerializeField, Range(0f, 1f)] private float walkableCollisionNormalY = 0.25f;
    [SerializeField] private bool resolveCollisionPenetration = true;
    [SerializeField] private int penetrationResolveIterations = 1;
    [SerializeField] private float penetrationSkin = 0.005f;
    [SerializeField] private float maxPenetrationCorrection = 0.05f;
    [SerializeField] private float obstacleAvoidanceDistance = 1.4f;
    [SerializeField] private float obstacleAvoidanceAngle = 55f;
    [SerializeField, Range(0f, 1f)] private float obstacleAvoidanceBlend = 0.75f;
    [SerializeField] private LayerMask searchLayers = ~0;
    [SerializeField] private Animator animator;
    [SerializeField] private string attackTrigger = "Attack";

    private const float FallbackAttackAnimationLength = 0.55f;
    private const float TargetSearchInterval = 0.2f;

    private MinionCombatant combatant;
    private MinionCombatant currentTarget;
    private TeamBuilding currentBuildingTarget;
    private Transform goal;
    private MinionBaseHealth enemyBase;
    private float nextAttackTime;
    private float nextMinionTargetSearchTime;
    private float nextBuildingTargetSearchTime;
    private float resumeWalkTime;
    private float attackAnimationLength = FallbackAttackAnimationLength;
    private int walkStateHash;
    private int attackStateHash;
    private bool attackAnimationPlaying;
    private CapsuleCollider capsuleCollider;
    private Collider selfCollider;

    public MinionKind Kind => kind;
    public MinionTeam Team => combatant != null ? combatant.Team : MinionTeam.Dog;

    private void Awake()
    {
        OnValidate();

        combatant = GetComponent<MinionCombatant>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        selfCollider = GetComponent<Collider>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        PrepareAnimator();
    }

    private void Update()
    {
        if (combatant == null || combatant.IsDead || (MinionManager.Instance != null && MinionManager.Instance.IsGameEnded))
        {
            return;
        }

        if (!IsValidTarget(currentTarget))
        {
            currentTarget = null;
        }

        if (!IsValidBuildingTarget(currentBuildingTarget) || !ShouldKeepBuildingTarget(currentBuildingTarget))
        {
            currentBuildingTarget = null;
        }

        if (currentBuildingTarget == null && Time.time >= nextBuildingTargetSearchTime)
        {
            currentBuildingTarget = FindNearestEnemyBuilding();
            nextBuildingTargetSearchTime = Time.time + TargetSearchInterval;
        }

        if (Time.time >= nextMinionTargetSearchTime)
        {
            currentTarget = FindNearestEnemyMinion();
            nextMinionTargetSearchTime = Time.time + TargetSearchInterval;
        }

        if (ShouldPrioritizeBuilding(currentBuildingTarget, currentTarget))
        {
            ChaseOrAttack(currentBuildingTarget);
            return;
        }

        if (currentTarget != null)
        {
            ChaseOrAttack(currentTarget);
            return;
        }

        if (currentBuildingTarget != null)
        {
            ChaseOrAttack(currentBuildingTarget);
            return;
        }

        if (TryAttackEnemyBase())
        {
            return;
        }

        MoveTowardGoal();
        KeepAnimatorMoving();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryMeleeHitBuilding(other))
        {
            TryEnterEnemyBase(other);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
        {
            return;
        }

        if (!TryMeleeHitBuilding(collision.collider))
        {
            TryEnterEnemyBase(collision.collider);
        }
    }

    private void LateUpdate()
    {
        ResolvePlayerOverlap();
        KeepAnimatorMoving();
    }

    public void Configure(
        MinionKind newKind,
        Transform newGoal,
        float newMoveSpeed,
        float newAttackRange,
        int newAttackDamage,
        int newBuildingAttackDamage,
        float newAttackCooldown,
        float newSearchRadius,
        float newBuildingSearchRadius,
        float newBuildingPriorityRadius,
        float newMinionInterruptRadius,
        MinionBaseHealth newEnemyBase)
    {
        kind = newKind;
        goal = newGoal;
        enemyBase = newEnemyBase;
        moveSpeed = Mathf.Max(0.1f, newMoveSpeed);
        attackRange = Mathf.Max(0.2f, newAttackRange);
        attackDamage = Mathf.Max(1, newAttackDamage);
        buildingAttackDamage = Mathf.Max(1, newBuildingAttackDamage);
        attackCooldown = Mathf.Max(0.1f, newAttackCooldown);
        targetSearchRadius = Mathf.Max(attackRange, newSearchRadius);
        buildingSearchRadius = Mathf.Max(targetSearchRadius, newBuildingSearchRadius);
        buildingPriorityRadius = Mathf.Clamp(newBuildingPriorityRadius, attackRange, buildingSearchRadius);
        minionInterruptRadius = Mathf.Max(0.1f, newMinionInterruptRadius);
    }

    private void ChaseOrAttack(MinionCombatant target)
    {
        Vector3 offset = target.transform.position - transform.position;
        offset.y = 0f;
        float attackDistance = GetAttackRangeForTarget(target);

        if (offset.sqrMagnitude > attackDistance * attackDistance)
        {
            MoveInDirection(offset.normalized);
            return;
        }

        FaceDirectionImmediate(offset);
        if (Time.time < nextAttackTime)
        {
            KeepAnimatorMoving();
            return;
        }

        nextAttackTime = Time.time + attackCooldown;
        PlayAttackAnimation();
        if (kind == MinionKind.Ranged)
        {
            MinionProjectile.Spawn(transform.position + Vector3.up * 0.9f, target, attackDamage, Team);
        }
        else
        {
            target.TakeDamage(attackDamage);
        }
    }

    private bool TryAttackEnemyBase()
    {
        if (enemyBase == null || enemyBase.IsDestroyed)
        {
            return false;
        }

        Vector3 offset = enemyBase.transform.position - transform.position;
        offset.y = 0f;

        if (!IsInsideEnemyBaseRange() && !FindOverlappingEnemyBase())
        {
            return false;
        }

        PlayAttackAnimation();
        FaceDirectionImmediate(offset);
        enemyBase.TakeDamage(attackDamage);
        Destroy(gameObject);
        return true;
    }

    private bool TryEnterEnemyBase(Collider other)
    {
        if (other == null || enemyBase == null || enemyBase.IsDestroyed)
        {
            return false;
        }

        MinionBaseHealth hitBase = other.GetComponentInParent<MinionBaseHealth>();
        if (hitBase == null || hitBase.Team == Team || hitBase.IsDestroyed)
        {
            return false;
        }

        PlayAttackAnimation();
        hitBase.TakeDamage(attackDamage);
        Destroy(gameObject);
        return true;
    }

    private void ChaseOrAttack(TeamBuilding targetBuilding)
    {
        BuildingHealth health = targetBuilding != null ? targetBuilding.Health : null;
        if (health == null || health.IsDestroyed)
        {
            currentBuildingTarget = null;
            return;
        }

        Vector3 offset = GetDirectionTowardBuildingTarget(health);

        if (!IsBuildingInAttackRange(health))
        {
            MoveInDirection(GetBuildingApproachDirection(health));
            return;
        }

        FaceDirectionImmediate(offset);
        if (Time.time < nextAttackTime)
        {
            KeepAnimatorMoving();
            return;
        }

        nextAttackTime = Time.time + attackCooldown;
        PlayAttackAnimation();
        if (kind == MinionKind.Ranged)
        {
            MinionProjectile.Spawn(transform.position + Vector3.up * 0.9f, health, buildingAttackDamage, Team);
        }
        else
        {
            health.TakeDamage(buildingAttackDamage);
        }
    }

    private bool TryMeleeHitBuilding(Collider other)
    {
        if (kind != MinionKind.Melee || other == null || Time.time < nextAttackTime)
        {
            return false;
        }

        TeamBuilding hitBuilding = other.GetComponentInParent<TeamBuilding>();
        if (!IsValidBuildingTarget(hitBuilding))
        {
            return false;
        }

        hitBuilding.Health.TakeDamage(buildingAttackDamage);
        nextAttackTime = Time.time + attackCooldown;
        PlayAttackAnimation();
        return true;
    }

    private void MoveTowardGoal()
    {
        if (goal == null)
        {
            return;
        }

        Vector3 offset = goal.position - transform.position;
        offset.y = 0f;
        float stopDistance = kind == MinionKind.Melee && enemyBase != null ? 0.1f : goalStopDistance;
        if (offset.sqrMagnitude <= stopDistance * stopDistance)
        {
            FaceDirection(offset);
            return;
        }

        MoveInDirection(offset.normalized);
    }

    private void MoveInDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Vector3 moveDirection = GetObstacleAvoidedDirection(direction.normalized);
        MoveWithCollision(moveDirection * moveSpeed * Time.deltaTime);
        attackAnimationPlaying = false;
        FaceDirection(moveDirection);
        SnapToGround();
    }

    public void ApplyKnockback(Vector3 direction, float distance)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f || distance <= 0f)
        {
            return;
        }

        MoveWithCollision(direction.normalized * distance);
        SnapToGround();
    }

    private Vector3 GetObstacleAvoidedDirection(Vector3 desiredDirection)
    {
        desiredDirection.y = 0f;
        if (desiredDirection.sqrMagnitude <= 0.001f || capsuleCollider == null)
        {
            return desiredDirection;
        }

        desiredDirection.Normalize();
        if (!HasAvoidanceObstacle(desiredDirection))
        {
            return desiredDirection;
        }

        Vector3 leftDirection = Quaternion.Euler(0f, -obstacleAvoidanceAngle, 0f) * desiredDirection;
        Vector3 rightDirection = Quaternion.Euler(0f, obstacleAvoidanceAngle, 0f) * desiredDirection;
        float leftClearance = GetObstacleClearance(leftDirection);
        float rightClearance = GetObstacleClearance(rightDirection);
        Vector3 avoidanceDirection = rightClearance > leftClearance ? rightDirection : leftDirection;

        if (Mathf.Abs(rightClearance - leftClearance) < 0.05f && GetInstanceID() % 2 == 0)
        {
            avoidanceDirection = rightDirection;
        }

        return Vector3.Slerp(desiredDirection, avoidanceDirection.normalized, obstacleAvoidanceBlend).normalized;
    }

    private bool HasAvoidanceObstacle(Vector3 direction)
    {
        return GetNearestAvoidanceHitDistance(direction, obstacleAvoidanceDistance, out _);
    }

    private float GetObstacleClearance(Vector3 direction)
    {
        if (GetNearestAvoidanceHitDistance(direction, obstacleAvoidanceDistance, out float distance))
        {
            return distance;
        }

        return obstacleAvoidanceDistance;
    }

    private bool GetNearestAvoidanceHitDistance(Vector3 direction, float distance, out float nearestDistance)
    {
        nearestDistance = float.PositiveInfinity;
        GetCapsuleWorldPoints(out Vector3 point1, out Vector3 point2, out float radius);
        RaycastHit[] hits = Physics.CapsuleCastAll(point1, point2, radius + collisionSkin, direction, distance, collisionLayers, QueryTriggerInteraction.Ignore);
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (!IsAvoidanceObstacle(hitCollider))
            {
                continue;
            }

            if (hits[i].distance < nearestDistance)
            {
                found = true;
                nearestDistance = hits[i].distance;
            }
        }

        return found;
    }

    private bool IsAvoidanceObstacle(Collider other)
    {
        if (other == null || other.transform.IsChildOf(transform) || other.isTrigger)
        {
            return false;
        }

        if (IsGroundLikeCollider(other) || IsBuildingCollider(other))
        {
            return false;
        }

        if (other.GetComponentInParent<MinionCombatant>() != null
            || other.GetComponentInParent<CatPlayerController>() != null
            || other.GetComponentInParent<MinionBaseHealth>() != null)
        {
            return false;
        }

        return true;
    }

    private void MoveWithCollision(Vector3 offset)
    {
        Vector3 horizontalOffset = new Vector3(offset.x, 0f, offset.z);
        if (horizontalOffset.sqrMagnitude > 0.000001f)
        {
            horizontalOffset = GetBlockedHorizontalOffset(horizontalOffset);
        }

        transform.position += horizontalOffset + Vector3.up * offset.y;
        ResolveCollisionPenetration();
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

    private void PlayAttackAnimation()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(attackTrigger) && HasTrigger(animator, attackTrigger))
        {
            animator.ResetTrigger(attackTrigger);
            animator.SetTrigger(attackTrigger);
        }

        if (attackStateHash != 0 && animator.HasState(0, attackStateHash))
        {
            animator.Play(attackStateHash, 0, 0f);
        }

        attackAnimationPlaying = true;
        resumeWalkTime = Mathf.Max(Time.time + attackAnimationLength, nextAttackTime - 0.05f);
    }

    private void PrepareAnimator()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;
        walkStateHash = Animator.StringToHash("Base Layer.Walk");
        attackStateHash = Animator.StringToHash("Base Layer.Attack");
        attackAnimationLength = FindClipLength("attack", FallbackAttackAnimationLength);

        PlayWalkAnimation(Random.value);
    }

    private void KeepAnimatorMoving()
    {
        if (animator == null || animator.runtimeAnimatorController == null || walkStateHash == 0 || !animator.HasState(0, walkStateHash))
        {
            return;
        }

        if (attackAnimationPlaying)
        {
            if (Time.time < resumeWalkTime)
            {
                return;
            }

            attackAnimationPlaying = false;
            PlayWalkAnimation(0f);
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (!state.IsName("Walk") || (!state.loop && state.normalizedTime >= 0.98f))
        {
            PlayWalkAnimation(0f);
        }
    }

    private void PlayWalkAnimation(float normalizedTime)
    {
        if (animator == null || animator.runtimeAnimatorController == null || walkStateHash == 0 || !animator.HasState(0, walkStateHash))
        {
            return;
        }

        animator.Play(walkStateHash, 0, normalizedTime);
        animator.Update(0f);
    }

    private float FindClipLength(string clipNamePart, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return fallback;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.name.ToLowerInvariant().Contains(clipNamePart))
            {
                return Mathf.Max(0.05f, clip.length);
            }
        }

        return fallback;
    }

    private static bool HasTrigger(Animator targetAnimator, string triggerName)
    {
        AnimatorControllerParameter[] parameters = targetAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.name == triggerName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                return true;
            }
        }

        return false;
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private void FaceDirectionImmediate(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * groundProbeHeight;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, groundProbeHeight + groundSnapDistance, ~0, QueryTriggerInteraction.Ignore);
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

            if (hits[i].distance < bestDistance)
            {
                foundGround = true;
                bestDistance = hits[i].distance;
                bestPoint = hits[i].point;
            }
        }

        if (foundGround)
        {
            float offsetY = bestPoint.y + groundSkin - GetBottomWorldY();
            if (Mathf.Abs(offsetY) > 0.001f)
            {
                transform.position += Vector3.up * offsetY;
            }

            ResolveCollisionPenetration();
        }
    }

    private float GetBottomWorldY()
    {
        if (capsuleCollider != null)
        {
            Vector3 worldCenter = transform.TransformPoint(capsuleCollider.center);
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            return worldCenter.y - capsuleCollider.height * scaleY * 0.5f;
        }

        return selfCollider != null ? selfCollider.bounds.min.y : transform.position.y;
    }

    private void ResolvePlayerOverlap()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, playerSeparationRadius, ~0, QueryTriggerInteraction.Collide);
        Vector3 push = Vector3.zero;

        for (int i = 0; i < hits.Length; i++)
        {
            CatPlayerController player = hits[i] != null ? hits[i].GetComponentInParent<CatPlayerController>() : null;
            if (player == null)
            {
                continue;
            }

            Vector3 away = transform.position - player.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude <= 0.001f)
            {
                away = Vector3.Cross(Vector3.up, player.transform.forward);
            }

            float distance = Mathf.Max(0.01f, away.magnitude);
            float strength = Mathf.Clamp01((playerSeparationRadius - distance) / playerSeparationRadius);
            push += away.normalized * strength;
        }

        if (push.sqrMagnitude <= 0.001f)
        {
            return;
        }

        MoveWithCollision(push.normalized * playerSeparationStrength * Time.deltaTime);
        SnapToGround();
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

    private MinionCombatant FindNearestEnemyMinion()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, targetSearchRadius, searchLayers, QueryTriggerInteraction.Collide);
        MinionCombatant nearest = null;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            MinionCombatant candidate = hits[i].GetComponentInParent<MinionCombatant>();
            if (!IsValidTarget(candidate))
            {
                continue;
            }

            Vector3 offset = candidate.transform.position - transform.position;
            offset.y = 0f;
            float distance = offset.sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private TeamBuilding FindNearestEnemyBuilding()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, buildingSearchRadius, searchLayers, QueryTriggerInteraction.Collide);
        TeamBuilding nearest = null;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            TeamBuilding candidate = hits[i] != null ? hits[i].GetComponentInParent<TeamBuilding>() : null;
            if (!IsValidBuildingTarget(candidate))
            {
                continue;
            }

            if (!IsBuildingAheadOfGoal(candidate))
            {
                continue;
            }

            Vector3 offset = candidate.transform.position - transform.position;
            offset.y = 0f;
            float distance = offset.sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private bool IsValidTarget(MinionCombatant candidate)
    {
        return candidate != null
            && candidate != combatant
            && !candidate.IsDead
            && candidate.Team != Team;
    }

    private bool IsValidBuildingTarget(TeamBuilding candidate)
    {
        return candidate != null
            && candidate.Team != Team
            && candidate.Health != null
            && !candidate.Health.IsDestroyed;
    }

    private bool ShouldPrioritizeBuilding(TeamBuilding building, MinionCombatant minion)
    {
        if (!IsValidBuildingTarget(building))
        {
            return false;
        }

        if (!IsValidTarget(minion))
        {
            return true;
        }

        float priorityDistance = GetPriorityDistanceForBuilding(building.Health);
        if (GetHorizontalDistanceToBuildingSqr(building.Health) > priorityDistance * priorityDistance)
        {
            return false;
        }

        Vector3 minionOffset = minion.transform.position - transform.position;
        minionOffset.y = 0f;
        return minionOffset.sqrMagnitude > minionInterruptRadius * minionInterruptRadius;
    }

    private bool ShouldKeepBuildingTarget(TeamBuilding building)
    {
        if (!IsValidBuildingTarget(building))
        {
            return false;
        }

        if (kind == MinionKind.Melee && IsInsideBuildingHorizontalFootprint(GetBuildingCollider(building.Health)))
        {
            return true;
        }

        return IsBuildingAheadOfGoal(building) || IsBuildingInAttackRange(building.Health);
    }

    private bool IsBuildingAheadOfGoal(TeamBuilding building)
    {
        if (building == null || goal == null)
        {
            return true;
        }

        Vector3 toGoal = goal.position - transform.position;
        toGoal.y = 0f;
        if (toGoal.sqrMagnitude <= 0.001f)
        {
            return true;
        }

        Vector3 toBuilding = building.transform.position - transform.position;
        toBuilding.y = 0f;
        if (toBuilding.sqrMagnitude <= 0.001f)
        {
            return true;
        }

        return Vector3.Dot(toGoal.normalized, toBuilding.normalized) >= -0.05f;
    }

    private float GetAttackRangeForTarget(MinionCombatant target)
    {
        Collider collider = target != null ? target.GetComponentInChildren<Collider>() : null;
        if (collider == null)
        {
            return attackRange;
        }

        return attackRange + Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
    }

    private float GetPriorityDistanceForBuilding(BuildingHealth targetBuilding)
    {
        return buildingPriorityRadius;
    }

    private bool IsBuildingInAttackRange(BuildingHealth targetBuilding)
    {
        if (targetBuilding == null)
        {
            return false;
        }

        Collider collider = GetBuildingCollider(targetBuilding);
        if (kind == MinionKind.Melee && IsInsideBuildingHorizontalFootprint(collider))
        {
            return false;
        }

        float distanceSqr = kind == MinionKind.Ranged
            ? GetHorizontalCenterDistanceToBuildingSqr(targetBuilding)
            : GetHorizontalDistanceToBuildingSqr(targetBuilding);

        return distanceSqr <= attackRange * attackRange;
    }

    private float GetHorizontalCenterDistanceToBuildingSqr(BuildingHealth targetBuilding)
    {
        if (targetBuilding == null)
        {
            return float.PositiveInfinity;
        }

        Vector3 offset = targetBuilding.transform.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude;
    }

    private float GetHorizontalDistanceToBuildingSqr(BuildingHealth targetBuilding)
    {
        Collider collider = GetBuildingCollider(targetBuilding);
        if (collider == null)
        {
            return GetHorizontalCenterDistanceToBuildingSqr(targetBuilding);
        }

        Vector3 closest = collider.ClosestPoint(transform.position);
        Vector3 offset = closest - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude;
    }

    private Vector3 GetBuildingApproachDirection(BuildingHealth targetBuilding)
    {
        Collider collider = GetBuildingCollider(targetBuilding);
        if (collider != null)
        {
            Vector3 closest = collider.ClosestPoint(transform.position);
            Vector3 toEdge = closest - transform.position;
            toEdge.y = 0f;
            if (toEdge.sqrMagnitude > 0.0001f)
            {
                return toEdge.normalized;
            }

            Vector3 awayFromCenter = transform.position - collider.bounds.center;
            awayFromCenter.y = 0f;
            if (awayFromCenter.sqrMagnitude > 0.0001f)
            {
                return awayFromCenter.normalized;
            }
        }

        return GetDirectionTowardBuildingTarget(targetBuilding);
    }

    private Vector3 GetDirectionTowardBuildingTarget(BuildingHealth targetBuilding)
    {
        if (targetBuilding == null)
        {
            return transform.forward;
        }

        Collider collider = GetBuildingCollider(targetBuilding);
        if (collider != null)
        {
            Vector3 closest = collider.ClosestPoint(transform.position);
            Vector3 toEdge = closest - transform.position;
            toEdge.y = 0f;
            if (toEdge.sqrMagnitude > 0.0001f)
            {
                return toEdge.normalized;
            }
        }

        Vector3 toCenter = targetBuilding.transform.position - transform.position;
        toCenter.y = 0f;
        return toCenter.sqrMagnitude > 0.0001f ? toCenter.normalized : transform.forward;
    }

    private Collider GetBuildingCollider(BuildingHealth targetBuilding)
    {
        return targetBuilding != null ? targetBuilding.GetComponentInChildren<Collider>() : null;
    }

    private bool IsInsideBuildingHorizontalFootprint(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        Vector3 closest = collider.ClosestPoint(transform.position);
        Vector3 toClosest = closest - transform.position;
        toClosest.y = 0f;
        if (toClosest.sqrMagnitude > 0.0001f)
        {
            return false;
        }

        Bounds bounds = collider.bounds;
        const float edgePadding = 0.05f;
        Vector3 position = transform.position;
        return position.x > bounds.min.x + edgePadding
            && position.x < bounds.max.x - edgePadding
            && position.z > bounds.min.z + edgePadding
            && position.z < bounds.max.z - edgePadding;
    }

    private bool IsInsideEnemyBaseRange()
    {
        Collider collider = enemyBase != null ? enemyBase.GetComponentInChildren<Collider>() : null;
        if (collider == null)
        {
            return false;
        }

        Vector3 closest = collider.ClosestPoint(transform.position);
        return (closest - transform.position).sqrMagnitude <= 0.0001f;
    }

    private bool FindOverlappingEnemyBase()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 0.55f, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            MinionBaseHealth hitBase = hits[i] != null ? hits[i].GetComponentInParent<MinionBaseHealth>() : null;
            if (hitBase != null && hitBase.Team != Team && !hitBase.IsDestroyed)
            {
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
        turnSpeed = Mathf.Max(0f, turnSpeed);
        targetSearchRadius = Mathf.Max(0.1f, targetSearchRadius);
        buildingSearchRadius = Mathf.Max(targetSearchRadius, buildingSearchRadius);
        attackRange = Mathf.Max(0.1f, attackRange);
        buildingPriorityRadius = Mathf.Clamp(buildingPriorityRadius, attackRange, buildingSearchRadius);
        minionInterruptRadius = Mathf.Max(0.1f, minionInterruptRadius);
        buildingAttackDamage = Mathf.Max(1, buildingAttackDamage);
        attackCooldown = Mathf.Max(0.05f, attackCooldown);
        groundProbeHeight = Mathf.Max(0.1f, groundProbeHeight);
        groundSnapDistance = Mathf.Max(0.1f, groundSnapDistance);
        groundSkin = Mathf.Max(0f, groundSkin);
        playerSeparationRadius = Mathf.Max(0f, playerSeparationRadius);
        playerSeparationStrength = Mathf.Max(0f, playerSeparationStrength);
        collisionSkin = Mathf.Max(0.001f, collisionSkin);
        walkableCollisionNormalY = Mathf.Clamp01(walkableCollisionNormalY);
        penetrationResolveIterations = Mathf.Clamp(penetrationResolveIterations, 1, 2);
        penetrationSkin = Mathf.Clamp(penetrationSkin, 0f, 0.02f);
        maxPenetrationCorrection = Mathf.Clamp(maxPenetrationCorrection, 0.01f, 0.1f);
        obstacleAvoidanceDistance = Mathf.Max(0.2f, obstacleAvoidanceDistance);
        obstacleAvoidanceAngle = Mathf.Clamp(obstacleAvoidanceAngle, 15f, 85f);
        obstacleAvoidanceBlend = Mathf.Clamp01(obstacleAvoidanceBlend);
    }
}
