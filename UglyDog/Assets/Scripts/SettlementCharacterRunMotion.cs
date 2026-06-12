using UnityEngine;

public class SettlementCharacterRunMotion : MonoBehaviour
{
    private const string RunStateName = "Run";
    private const string SpeedParameterName = "Speed";
    private const float RunRestartThreshold = 0.9f;
    private const float RunBlendDuration = 0.08f;
    private const float SlowMotionAnimatorSpeed = 0.42f;

    private Vector3 lockedPosition;
    private Quaternion lockedRotation;
    private Vector3 lockedScale;
    private Animator[] animators;

    private void Awake()
    {
        LockCurrentTransform();
        PrepareAnimators();
    }

    private void OnEnable()
    {
        LockCurrentTransform();
        PrepareAnimators();
        PlayRunAnimations(0f);
    }

    private void LateUpdate()
    {
        transform.position = lockedPosition;
        transform.rotation = lockedRotation;
        transform.localScale = lockedScale;
        KeepRunAnimationsLooping();
    }

    private void LockCurrentTransform()
    {
        lockedPosition = transform.position;
        lockedRotation = transform.rotation;
        lockedScale = transform.localScale;
    }

    private void PrepareAnimators()
    {
        animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
            {
                continue;
            }

            animator.enabled = true;
            animator.speed = SlowMotionAnimatorSpeed;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            SetFloatIfExists(animator, SpeedParameterName, 1f);
            ResetTriggerIfExists(animator, "Attack");
            ResetTriggerIfExists(animator, "Dig");
            ResetTriggerIfExists(animator, "Build");
        }
    }

    private void KeepRunAnimationsLooping()
    {
        if (animators == null || animators.Length == 0)
        {
            PrepareAnimators();
        }

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                continue;
            }

            SetFloatIfExists(animator, SpeedParameterName, 1f);
            animator.speed = SlowMotionAnimatorSpeed;

            int runHash = Animator.StringToHash("Base Layer." + RunStateName);
            if (!animator.HasState(0, runHash))
            {
                continue;
            }

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.fullPathHash != runHash)
            {
                animator.CrossFade(runHash, RunBlendDuration, 0, 0f);
                continue;
            }

            if (!stateInfo.loop && !animator.IsInTransition(0) && stateInfo.normalizedTime >= RunRestartThreshold)
            {
                animator.CrossFade(runHash, RunBlendDuration, 0, 0f);
            }
        }
    }

    private void PlayRunAnimations(float normalizedTime)
    {
        if (animators == null)
        {
            return;
        }

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                continue;
            }

            int runHash = Animator.StringToHash("Base Layer." + RunStateName);
            if (animator.HasState(0, runHash))
            {
                animator.Play(runHash, 0, normalizedTime);
            }
        }
    }

    private static void SetFloatIfExists(Animator animator, string parameterName, float value)
    {
        if (animator.runtimeAnimatorController == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
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

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.ResetTrigger(parameterName);
                return;
            }
        }
    }

}
