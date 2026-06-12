using UnityEngine;

public class VictoryCharacterPreviewMotion : MonoBehaviour
{
    [SerializeField] private string actionStateName = "Attack";
    [SerializeField] private float actionInterval = 1.8f;
    [SerializeField] private float turnAmplitude = 7f;
    [SerializeField] private float bobAmplitude = 0.035f;
    [SerializeField] private float bobSpeed = 2.2f;

    private Animator animator;
    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private float nextActionTime;

    public void Configure(string stateName)
    {
        actionStateName = string.IsNullOrWhiteSpace(stateName) ? "Attack" : stateName;
        CacheAnimator();
        PlayAction();
    }

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        CacheAnimator();
    }

    private void OnEnable()
    {
        nextActionTime = Time.unscaledTime + 0.15f;
    }

    private void Update()
    {
        float phase = Time.unscaledTime * bobSpeed;
        transform.localPosition = baseLocalPosition + Vector3.up * (Mathf.Sin(phase) * bobAmplitude);
        transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, Mathf.Sin(phase * 0.65f) * turnAmplitude, 0f);

        if (Time.unscaledTime >= nextActionTime)
        {
            PlayAction();
            nextActionTime = Time.unscaledTime + actionInterval;
        }
    }

    private void CacheAnimator()
    {
        animator = GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            return;
        }

        animator.enabled = true;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    private void PlayAction()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        int stateHash = Animator.StringToHash("Base Layer." + actionStateName);
        if (animator.HasState(0, stateHash))
        {
            animator.CrossFadeInFixedTime(stateHash, 0.08f, 0, 0f);
            return;
        }

        for (int i = 0; i < animator.parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = animator.parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == actionStateName)
            {
                animator.ResetTrigger(parameter.nameHash);
                animator.SetTrigger(parameter.nameHash);
                return;
            }
        }
    }
}
