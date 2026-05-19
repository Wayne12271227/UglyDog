using UnityEngine;

[RequireComponent(typeof(MinionCombatant))]
public class MinionUnit : MonoBehaviour
{
    [SerializeField] private MinionKind kind = MinionKind.Melee;
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float turnSpeed = 12f;
    [SerializeField] private float targetSearchRadius = 7f;
    [SerializeField] private float attackRange = 1.25f;
    [SerializeField] private int attackDamage = 4;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float goalStopDistance = 1.8f;
    [SerializeField] private float groundProbeHeight = 3f;
    [SerializeField] private float groundSnapDistance = 6f;
    [SerializeField] private float playerSeparationRadius = 0.95f;
    [SerializeField] private float playerSeparationStrength = 4f;
    [SerializeField] private LayerMask searchLayers = ~0;
    [SerializeField] private Animator animator;
    [SerializeField] private string attackTrigger = "Attack";

    private const float FallbackAttackAnimationLength = 0.55f;

    private MinionCombatant combatant;
    private MinionCombatant currentTarget;
    private TeamBuilding currentBuildingTarget;
    private Transform goal;
    private MinionBaseHealth enemyBase;
    private float nextAttackTime;
    private float nextTargetSearchTime;
    private float resumeWalkTime;
    private float attackAnimationLength = FallbackAttackAnimationLength;
    private int walkStateHash;
    private int attackStateHash;
    private bool attackAnimationPlaying;

    public MinionKind Kind => kind;
    public MinionTeam Team => combatant != null ? combatant.Team : MinionTeam.Dog;

    private void Awake()
    {
        combatant = GetComponent<MinionCombatant>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        PrepareAnimator();
    }

    private void Update()
    {
        if (combatant == null || combatant.IsDead)
        {
            return;
        }

        if (!IsValidTarget(currentTarget) || Time.time >= nextTargetSearchTime)
        {
            currentTarget = FindNearestEnemyMinion();
            nextTargetSearchTime = Time.time + 0.2f;
        }

        if (currentTarget != null)
        {
            currentBuildingTarget = null;
            ChaseOrAttack(currentTarget);
            return;
        }

        if (!IsValidBuildingTarget(currentBuildingTarget) || Time.time >= nextTargetSearchTime)
        {
            currentBuildingTarget = FindNearestEnemyBuilding();
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
            TryMeleeHitBase(other);
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
            TryMeleeHitBase(collision.collider);
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
        float newAttackCooldown,
        float newSearchRadius,
        MinionBaseHealth newEnemyBase)
    {
        kind = newKind;
        goal = newGoal;
        enemyBase = newEnemyBase;
        moveSpeed = Mathf.Max(0.1f, newMoveSpeed);
        attackRange = Mathf.Max(0.2f, newAttackRange);
        attackDamage = Mathf.Max(1, newAttackDamage);
        attackCooldown = Mathf.Max(0.1f, newAttackCooldown);
        targetSearchRadius = Mathf.Max(attackRange, newSearchRadius);
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

        if (kind == MinionKind.Melee)
        {
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

        float attackDistance = GetAttackRangeForBase(enemyBase);
        if (offset.sqrMagnitude > attackDistance * attackDistance)
        {
            return false;
        }

        FaceDirectionImmediate(offset);
        if (Time.time < nextAttackTime)
        {
            KeepAnimatorMoving();
            return true;
        }

        nextAttackTime = Time.time + attackCooldown;
        PlayAttackAnimation();
        MinionProjectile.Spawn(transform.position + Vector3.up * 0.9f, enemyBase, attackDamage, Team);
        return true;
    }

    private bool TryMeleeHitBase(Collider other)
    {
        if (kind != MinionKind.Melee || other == null || enemyBase == null || enemyBase.IsDestroyed)
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

        Vector3 offset = targetBuilding.transform.position - transform.position;
        offset.y = 0f;
        float attackDistance = GetAttackRangeForBuilding(health);

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
            MinionProjectile.Spawn(transform.position + Vector3.up * 0.9f, health, attackDamage, Team);
        }
        else
        {
            health.TakeDamage(attackDamage);
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

        hitBuilding.Health.TakeDamage(attackDamage);
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

        transform.position += direction * moveSpeed * Time.deltaTime;
        attackAnimationPlaying = false;
        FaceDirection(direction);
        SnapToGround();
    }

    public void ApplyKnockback(Vector3 direction, float distance)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f || distance <= 0f)
        {
            return;
        }

        transform.position += direction.normalized * distance;
        SnapToGround();
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

            if (hits[i].distance < bestDistance)
            {
                foundGround = true;
                bestDistance = hits[i].distance;
                bestPoint = hits[i].point;
            }
        }

        if (foundGround)
        {
            Collider selfCollider = GetComponent<Collider>();
            float centerToBottom = selfCollider != null ? transform.position.y - selfCollider.bounds.min.y : 0f;
            transform.position = new Vector3(transform.position.x, bestPoint.y + centerToBottom + 0.02f, transform.position.z);
        }
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

        transform.position += push.normalized * playerSeparationStrength * Time.deltaTime;
        SnapToGround();
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
        Collider[] hits = Physics.OverlapSphere(transform.position, targetSearchRadius, searchLayers, QueryTriggerInteraction.Collide);
        TeamBuilding nearest = null;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            TeamBuilding candidate = hits[i] != null ? hits[i].GetComponentInParent<TeamBuilding>() : null;
            if (!IsValidBuildingTarget(candidate))
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

    private float GetAttackRangeForTarget(MinionCombatant target)
    {
        Collider collider = target != null ? target.GetComponentInChildren<Collider>() : null;
        if (collider == null)
        {
            return attackRange;
        }

        return attackRange + Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
    }

    private float GetAttackRangeForBase(MinionBaseHealth targetBase)
    {
        Collider collider = targetBase != null ? targetBase.GetComponentInChildren<Collider>() : null;
        if (collider == null)
        {
            return attackRange;
        }

        return attackRange + Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
    }

    private float GetAttackRangeForBuilding(BuildingHealth targetBuilding)
    {
        Collider collider = targetBuilding != null ? targetBuilding.GetComponentInChildren<Collider>() : null;
        if (collider == null)
        {
            return attackRange;
        }

        return attackRange + Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
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
}
