using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class CatAnimatorControllerBuilder : EditorWindow
{
    private const string ControllerFolder = "Assets/animator";
    private const string ControllerPath = ControllerFolder + "/CatActions.controller";
    private const float RunStateSpeed = 2.5f;
    private const float AttackStateSpeed = 1.5f;
    private const float ActionStateSpeed = 3.5f;

    private GameObject characterRoot;
    private AnimationClip idleClip;
    private AnimationClip runClip;
    private AnimationClip attackClip;
    private AnimationClip digClip;
    private AnimationClip buildClip;

    [MenuItem("Tools/Cat Animator Setup")]
    public static void Open()
    {
        GetWindow<CatAnimatorControllerBuilder>("Cat Animator Setup");
    }

    private void OnEnable()
    {
        if (Selection.activeGameObject != null)
        {
            characterRoot = Selection.activeGameObject;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Cat Animator Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select the new cat root and assign clips for Idle, Run, Attack, Dig, and Build. Missing clips will get empty placeholder states so the controller can still be wired.", MessageType.Info);

        characterRoot = (GameObject)EditorGUILayout.ObjectField("Character Root", characterRoot, typeof(GameObject), true);
        idleClip = (AnimationClip)EditorGUILayout.ObjectField("Idle Clip", idleClip, typeof(AnimationClip), false);
        runClip = (AnimationClip)EditorGUILayout.ObjectField("Run Clip", runClip, typeof(AnimationClip), false);
        attackClip = (AnimationClip)EditorGUILayout.ObjectField("Attack Clip", attackClip, typeof(AnimationClip), false);
        digClip = (AnimationClip)EditorGUILayout.ObjectField("Dig Clip", digClip, typeof(AnimationClip), false);
        buildClip = (AnimationClip)EditorGUILayout.ObjectField("Build Clip", buildClip, typeof(AnimationClip), false);

        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(characterRoot == null))
        {
            if (GUILayout.Button("Build And Assign 5 Action Animator", GUILayout.Height(32f)))
            {
                BuildAndAssign();
            }
        }
    }

    private void BuildAndAssign()
    {
        Directory.CreateDirectory(ControllerFolder);

        string path = AssetDatabase.GenerateUniqueAssetPath(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dig", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Build", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = CreateState(stateMachine, "Idle", idleClip, new Vector3(240f, 100f, 0f), true);
        AnimatorState runState = CreateState(stateMachine, "Run", runClip, new Vector3(480f, 100f, 0f), true);
        AnimatorState attackState = CreateState(stateMachine, "Attack", attackClip, new Vector3(360f, 260f, 0f), false);
        AnimatorState digState = CreateState(stateMachine, "Dig", digClip, new Vector3(560f, 260f, 0f), false);
        AnimatorState buildState = CreateState(stateMachine, "Build", buildClip, new Vector3(760f, 260f, 0f), false);
        runState.speed = RunStateSpeed;
        attackState.speed = AttackStateSpeed;
        digState.speed = ActionStateSpeed;
        buildState.speed = ActionStateSpeed;
        stateMachine.defaultState = idleState;

        AddSpeedTransition(idleState, runState, AnimatorConditionMode.Greater, 0.1f);
        AddSpeedTransition(runState, idleState, AnimatorConditionMode.Less, 0.1f);
        AddActionTransition(stateMachine, attackState, "Attack");
        AddActionTransition(stateMachine, digState, "Dig");
        AddActionTransition(stateMachine, buildState, "Build");
        AddReturnTransition(attackState, idleState);
        AddReturnTransition(digState, idleState);
        AddReturnTransition(buildState, idleState);

        Animator animator = characterRoot.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(characterRoot);
        }

        Undo.RecordObject(animator, "Assign Cat Animator Controller");
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        CatPlayerController playerController = characterRoot.GetComponent<CatPlayerController>();
        if (playerController == null)
        {
            playerController = Undo.AddComponent<CatPlayerController>(characterRoot);
        }

        SerializedObject serializedPlayer = new SerializedObject(playerController);
        serializedPlayer.FindProperty("animator").objectReferenceValue = animator;
        serializedPlayer.FindProperty("speedParameter").stringValue = "Speed";
        serializedPlayer.FindProperty("attackTrigger").stringValue = "Attack";
        serializedPlayer.FindProperty("digTrigger").stringValue = "Dig";
        serializedPlayer.FindProperty("buildTrigger").stringValue = "Build";
        serializedPlayer.ApplyModifiedProperties();

        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(playerController);
        AssetDatabase.SaveAssets();

        Selection.activeObject = controller;
        EditorUtility.DisplayDialog("Cat Animator Setup", "Done. CatActions controller was created and assigned.", "OK");
    }

    private static AnimatorState CreateState(AnimatorStateMachine stateMachine, string stateName, AnimationClip clip, Vector3 position, bool loop)
    {
        AnimatorState state = stateMachine.AddState(stateName, position);
        AnimationClip motion = clip != null ? clip : CreateEmptyClip(stateName, loop);
        state.motion = motion;
        SetClipLoop(motion, loop);
        return state;
    }

    private static void AddSpeedTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.18f;
        transition.AddCondition(mode, threshold, "Speed");
    }

    private static void AddActionTransition(AnimatorStateMachine stateMachine, AnimatorState actionState, string triggerName)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(actionState);
        transition.hasExitTime = false;
        transition.duration = 0.08f;
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

    private static AnimationClip CreateEmptyClip(string stateName, bool loop)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath(ControllerFolder + "/Cat" + stateName + "Placeholder.anim");
        AnimationClip clip = new AnimationClip
        {
            name = "Cat" + stateName + "Placeholder",
            frameRate = 30f
        };

        AssetDatabase.CreateAsset(clip, path);
        SetClipLoop(clip, loop);
        return clip;
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
}
