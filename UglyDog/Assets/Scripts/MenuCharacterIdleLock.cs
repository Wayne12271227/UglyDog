using UnityEngine;

public class MenuCharacterIdleLock : MonoBehaviour
{
    private const string IdleStateName = "Idle";
    private const float IdleRestartThreshold = 0.88f;
    private const float IdleBlendDuration = 0.18f;

    private Vector3 lockedPosition;
    private Quaternion lockedRotation;
    private Vector3 lockedScale;
    private Animator[] animators;

    private void Awake()
    {
        LockCurrentTransform();
        PreparePhysics();
        PrepareAnimator();
    }

    private void OnEnable()
    {
        LockCurrentTransform();
    }

    private void LateUpdate()
    {
        transform.position = lockedPosition;
        transform.rotation = lockedRotation;
        transform.localScale = lockedScale;
        KeepIdleAnimationsLooping();
    }

    private void LockCurrentTransform()
    {
        lockedPosition = transform.position;
        lockedRotation = transform.rotation;
        lockedScale = transform.localScale;
    }

    private void PreparePhysics()
    {
        foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }
    }

    private void PrepareAnimator()
    {
        animators = GetComponentsInChildren<Animator>(true);
        foreach (Animator animator in animators)
        {
            animator.applyRootMotion = false;
            SetFloatIfExists(animator, "Speed", 0f);
            ResetTriggerIfExists(animator, "Attack");
            ResetTriggerIfExists(animator, "Dig");
            ResetTriggerIfExists(animator, "Build");
            PlayStateIfExists(animator, IdleStateName, 0f);
        }
    }

    private void KeepIdleAnimationsLooping()
    {
        if (animators == null || animators.Length == 0)
        {
            animators = GetComponentsInChildren<Animator>(true);
        }

        foreach (Animator animator in animators)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                continue;
            }

            SetFloatIfExists(animator, "Speed", 0f);

            int idleHash = Animator.StringToHash("Base Layer." + IdleStateName);
            if (!animator.HasState(0, idleHash))
            {
                continue;
            }

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.fullPathHash != idleHash)
            {
                animator.CrossFade(idleHash, 0.08f, 0, 0f);
                continue;
            }

            if (!stateInfo.loop && !animator.IsInTransition(0) && stateInfo.normalizedTime >= IdleRestartThreshold)
            {
                animator.CrossFade(idleHash, IdleBlendDuration, 0, 0f);
            }
        }
    }

    private static void SetFloatIfExists(Animator animator, string parameterName, float value)
    {
        if (animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Float)
            {
                animator.SetFloat(parameterName, value);
                return;
            }
        }
    }

    private static void ResetTriggerIfExists(Animator animator, string parameterName)
    {
        if (animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.ResetTrigger(parameterName);
                return;
            }
        }
    }

    private static void PlayStateIfExists(Animator animator, string stateName, float normalizedTime)
    {
        if (animator.runtimeAnimatorController == null)
        {
            return;
        }

        int stateHash = Animator.StringToHash("Base Layer." + stateName);
        if (animator.HasState(0, stateHash))
        {
            animator.Play(stateHash, 0, normalizedTime);
        }
    }
}
