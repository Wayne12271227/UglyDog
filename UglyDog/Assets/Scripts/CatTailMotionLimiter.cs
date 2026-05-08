using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class CatTailMotionLimiter : MonoBehaviour
{
    [SerializeField] private Transform[] tailTransforms;
    [SerializeField] private string[] fallbackTargetNames =
    {
        "Tail",
        "tail",
        "tripo_part_15",
        "tripo_part_16",
        "tripo_part_17",
        "tripo_part_18"
    };

    [SerializeField] private bool lockTailToRestPose = true;
    [SerializeField, Range(0f, 1f)] private float rotationMotionScale = 0f;
    [SerializeField, Range(0f, 1f)] private float verticalMotionScale = 0f;
    [SerializeField] private bool onlyWhileRunning = true;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string runStateName = "Run";
    [SerializeField, Range(0f, 1f)] private float minimumRunWeight = 0.08f;
    [SerializeField, Range(0f, 0.2f)] private float groundClearance = 0.04f;
    [SerializeField, Range(0f, 0.2f)] private float maxGroundLiftPerFrame = 0.08f;

    private readonly List<TailTarget> targets = new List<TailTarget>();
    private Animator animator;
    private int speedHash;
    private int runStateHash;
    private bool hasSpeedParameter;
    private float groundOffsetFromRoot;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        runStateHash = Animator.StringToHash(runStateName);
        CacheAnimatorParameters();
        BuildTargets();
        CaptureGroundOffset();
    }

    private void LateUpdate()
    {
        if (targets.Count == 0)
        {
            BuildTargets();
            if (targets.Count == 0)
            {
                return;
            }
        }

        float weight = GetLimiterWeight();
        if (weight <= 0f)
        {
            return;
        }

        float groundY = transform.position.y + groundOffsetFromRoot + groundClearance;
        for (int i = 0; i < targets.Count; i++)
        {
            ApplyMotionLimit(targets[i], weight);
            LiftAboveGroundIfNeeded(targets[i], groundY, weight);
        }
    }

    private void CacheAnimatorParameters()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        speedHash = Animator.StringToHash(speedParameter);
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == speedParameter && parameter.type == AnimatorControllerParameterType.Float)
            {
                hasSpeedParameter = true;
                return;
            }
        }
    }

    private void BuildTargets()
    {
        targets.Clear();

        if (tailTransforms != null)
        {
            for (int i = 0; i < tailTransforms.Length; i++)
            {
                AddTarget(tailTransforms[i]);
            }
        }

        if (targets.Count == 0)
        {
            AddTargetsByName();
        }

        if (targets.Count == 0)
        {
            AddLikelyRearParts();
        }
    }

    private void AddTargetsByName()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == transform || IsOutlineTransform(child))
            {
                continue;
            }

            for (int j = 0; j < fallbackTargetNames.Length; j++)
            {
                string targetName = fallbackTargetNames[j];
                if (!string.IsNullOrEmpty(targetName) && child.name == targetName)
                {
                    AddTarget(child);
                    break;
                }
            }
        }
    }

    private void AddLikelyRearParts()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        List<Renderer> candidates = new List<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.GetComponent<CharacterOutlineProxy>() != null)
            {
                continue;
            }

            Vector3 localCenter = transform.InverseTransformPoint(renderer.bounds.center);
            if (localCenter.z < -0.03f && localCenter.y > 0.15f)
            {
                candidates.Add(renderer);
            }
        }

        candidates.Sort((left, right) =>
        {
            float leftZ = transform.InverseTransformPoint(left.bounds.center).z;
            float rightZ = transform.InverseTransformPoint(right.bounds.center).z;
            return leftZ.CompareTo(rightZ);
        });

        int count = Mathf.Min(4, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            AddTarget(candidates[i].transform);
        }
    }

    private void AddTarget(Transform target)
    {
        if (target == null || ContainsTarget(target))
        {
            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        targets.Add(new TailTarget(target, target.localPosition, target.localRotation, renderers));
    }

    private bool ContainsTarget(Transform target)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].Transform == target)
            {
                return true;
            }
        }

        return false;
    }

    private void CaptureGroundOffset()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool foundRenderer = false;
        float minY = transform.position.y;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.GetComponent<CharacterOutlineProxy>() != null)
            {
                continue;
            }

            minY = foundRenderer ? Mathf.Min(minY, renderer.bounds.min.y) : renderer.bounds.min.y;
            foundRenderer = true;
        }

        groundOffsetFromRoot = minY - transform.position.y;
    }

    private float GetLimiterWeight()
    {
        if (!onlyWhileRunning)
        {
            return 1f;
        }

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return 1f;
        }

        float speedWeight = 0f;
        if (hasSpeedParameter)
        {
            float speed = animator.GetFloat(speedHash);
            speedWeight = Mathf.InverseLerp(minimumRunWeight, 1f, speed);
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (currentState.shortNameHash == runStateHash)
        {
            speedWeight = Mathf.Max(speedWeight, 1f);
        }

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
            if (nextState.shortNameHash == runStateHash)
            {
                speedWeight = Mathf.Max(speedWeight, 1f);
            }
        }

        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(speedWeight));
    }

    private void ApplyMotionLimit(TailTarget target, float weight)
    {
        Transform targetTransform = target.Transform;
        if (lockTailToRestPose)
        {
            targetTransform.localRotation = Quaternion.Slerp(targetTransform.localRotation, target.BaseLocalRotation, weight);
            targetTransform.localPosition = Vector3.Lerp(targetTransform.localPosition, target.BaseLocalPosition, weight);
            return;
        }

        Quaternion limitedRotation = Quaternion.Slerp(target.BaseLocalRotation, targetTransform.localRotation, rotationMotionScale);
        targetTransform.localRotation = Quaternion.Slerp(targetTransform.localRotation, limitedRotation, weight);

        Vector3 currentPosition = targetTransform.localPosition;
        Vector3 basePosition = target.BaseLocalPosition;
        Vector3 limitedPosition = currentPosition;
        limitedPosition.y = basePosition.y + (currentPosition.y - basePosition.y) * verticalMotionScale;
        targetTransform.localPosition = Vector3.Lerp(currentPosition, limitedPosition, weight);
    }

    private void LiftAboveGroundIfNeeded(TailTarget target, float groundY, float weight)
    {
        Renderer[] renderers = target.Renderers;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            float penetration = groundY - renderer.bounds.min.y;
            if (penetration <= 0f)
            {
                continue;
            }

            float lift = Mathf.Min(penetration, maxGroundLiftPerFrame) * weight;
            target.Transform.position += Vector3.up * lift;
        }
    }

    private static bool IsOutlineTransform(Transform target)
    {
        return target.name.EndsWith("__Outline") || target.GetComponent<CharacterOutlineProxy>() != null;
    }

    private struct TailTarget
    {
        public readonly Transform Transform;
        public readonly Vector3 BaseLocalPosition;
        public readonly Quaternion BaseLocalRotation;
        public readonly Renderer[] Renderers;

        public TailTarget(Transform transform, Vector3 baseLocalPosition, Quaternion baseLocalRotation, Renderer[] renderers)
        {
            Transform = transform;
            BaseLocalPosition = baseLocalPosition;
            BaseLocalRotation = baseLocalRotation;
            Renderers = renderers;
        }
    }
}
