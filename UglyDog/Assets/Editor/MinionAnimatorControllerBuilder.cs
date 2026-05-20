using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class MinionAnimatorControllerBuilder : EditorWindow
{
    private const string AnimatorRootFolder = "Assets/animator";
    private const string MinionControllerFolder = AnimatorRootFolder + "/minion_action";
    private const float WalkStateSpeed = 1.2f;
    private const float AttackStateSpeed = 1.2f;

    private GameObject minionModel;
    private AnimationClip idleClip;
    private AnimationClip walkClip;
    private AnimationClip attackClip;
    private string controllerName = "MinionActions";

    [MenuItem("Tools/Minions/Minion Animator Setup")]
    public static void Open()
    {
        GetWindow<MinionAnimatorControllerBuilder>("Minion Animator Setup");
    }

    private void OnEnable()
    {
        if (Selection.activeGameObject != null)
        {
            minionModel = Selection.activeGameObject;
            controllerName = SanitizeAssetName(minionModel.name) + "Actions";
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Minion Animator Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Builds a minion controller with Idle, Walk, and Attack. Drag a scene minion, prefab, or model asset, then assign walk and attack clips.", MessageType.Info);

        EditorGUI.BeginChangeCheck();
        minionModel = (GameObject)EditorGUILayout.ObjectField("Minion Model", minionModel, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() && minionModel != null)
        {
            controllerName = SanitizeAssetName(minionModel.name) + "Actions";
        }

        controllerName = EditorGUILayout.TextField("Controller Name", controllerName);
        idleClip = (AnimationClip)EditorGUILayout.ObjectField("Idle Clip", idleClip, typeof(AnimationClip), false);
        walkClip = (AnimationClip)EditorGUILayout.ObjectField("Walk Clip", walkClip, typeof(AnimationClip), false);
        attackClip = (AnimationClip)EditorGUILayout.ObjectField("Attack Clip", attackClip, typeof(AnimationClip), false);

        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(minionModel == null))
        {
            if (GUILayout.Button("Build And Assign Minion Animator", GUILayout.Height(32f)))
            {
                BuildAndAssign();
            }
        }
    }

    private void BuildAndAssign()
    {
        EnsureFolder(MinionControllerFolder);

        string safeControllerName = string.IsNullOrWhiteSpace(controllerName)
            ? SanitizeAssetName(minionModel.name) + "Actions"
            : SanitizeAssetName(controllerName);
        string controllerPath = MinionControllerFolder + "/" + safeControllerName + ".controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        RebuildController(controller, safeControllerName);

        if (IsSceneObject(minionModel))
        {
            AssignControllerToSceneObject(minionModel, controller);
        }
        else
        {
            CreatePrefabWithController(minionModel, controller, safeControllerName);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = controller;
        EditorUtility.DisplayDialog("Minion Animator Setup", "Done. " + controller.name + " was created and assigned.", "OK");
    }

    private void RebuildController(AnimatorController controller, string clipPrefix)
    {
        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState state in stateMachine.states)
        {
            stateMachine.RemoveState(state.state);
        }

        foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
        {
            stateMachine.RemoveStateMachine(childMachine.stateMachine);
        }

        AnimatorState idleState = CreateState(stateMachine, "Idle", idleClip, new Vector3(240f, 100f, 0f), true, clipPrefix);
        AnimatorState walkState = CreateState(stateMachine, "Walk", walkClip, new Vector3(480f, 100f, 0f), true, clipPrefix);
        AnimatorState attackState = CreateState(stateMachine, "Attack", attackClip, new Vector3(360f, 260f, 0f), false, clipPrefix);

        walkState.speed = WalkStateSpeed;
        attackState.speed = AttackStateSpeed;
        stateMachine.defaultState = idleState;

        AddSpeedTransition(idleState, walkState, AnimatorConditionMode.Greater, 0.1f);
        AddSpeedTransition(walkState, idleState, AnimatorConditionMode.Less, 0.1f);
        AddActionTransition(stateMachine, attackState, "Attack");
        AddReturnTransition(attackState, idleState);

        EditorUtility.SetDirty(controller);
    }

    private static AnimatorState CreateState(AnimatorStateMachine stateMachine, string stateName, AnimationClip clip, Vector3 position, bool loop, string clipPrefix)
    {
        AnimatorState state = stateMachine.AddState(stateName, position);
        AnimationClip motion = clip != null ? clip : CreateEmptyClip(stateName, loop, clipPrefix);
        state.motion = motion;
        SetClipLoop(motion, loop);
        return state;
    }

    private static void AddSpeedTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.16f;
        transition.AddCondition(mode, threshold, "Speed");
    }

    private static void AddActionTransition(AnimatorStateMachine stateMachine, AnimatorState actionState, string triggerName)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(actionState);
        transition.hasExitTime = false;
        transition.duration = 0.06f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
    }

    private static void AddReturnTransition(AnimatorState from, AnimatorState idleState)
    {
        AnimatorStateTransition transition = from.AddTransition(idleState);
        transition.hasExitTime = true;
        transition.exitTime = 0.95f;
        transition.duration = 0.08f;
    }

    private static AnimationClip CreateEmptyClip(string stateName, bool loop, string clipPrefix)
    {
        string clipName = clipPrefix + stateName + "Placeholder";
        string path = MinionControllerFolder + "/" + clipName + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip
            {
                name = clipName,
                frameRate = 30f
            };

            AssetDatabase.CreateAsset(clip, path);
        }

        SetClipLoop(clip, loop);
        return clip;
    }

    private static void AssignControllerToSceneObject(GameObject target, RuntimeAnimatorController controller)
    {
        Animator animator = target.GetComponent<Animator>();
        if (animator == null)
        {
            animator = target.GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(target);
        }

        Undo.RecordObject(animator, "Assign Minion Animator Controller");
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        EditorUtility.SetDirty(animator);
    }

    private static void CreatePrefabWithController(GameObject modelAsset, RuntimeAnimatorController controller, string safeControllerName)
    {
        string prefabFolder = "Assets/prefab/minions";
        EnsureFolder(prefabFolder);

        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        if (root == null)
        {
            root = Instantiate(modelAsset);
        }

        root.name = modelAsset.name + " Animated";
        Animator animator = root.GetComponent<Animator>();
        if (animator == null)
        {
            animator = root.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        string prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabFolder + "/" + safeControllerName + ".prefab");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        DestroyImmediate(root);
        if (prefab != null)
        {
            Selection.activeObject = prefab;
        }
    }

    private static bool IsSceneObject(GameObject target)
    {
        return target != null && !EditorUtility.IsPersistent(target);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static void SetClipLoop(AnimationClip clip, bool loop)
    {
        if (clip == null)
        {
            return;
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static string SanitizeAssetName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Minion";
        }

        foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid.ToString(), string.Empty);
        }

        return value.Replace(" ", string.Empty);
    }
}
