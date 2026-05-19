using System.IO;
using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class CharacterAnimatorControllerBuilder : EditorWindow
{
    private enum CharacterKind
    {
        Cat,
        Dog,
        DogMeleeMinion,
        DogRangedMinion,
        CatMeleeMinion,
        CatRangedMinion
    }

    private const string AnimatorRootFolder = "Assets/animator";
    private const string CatControllerFolder = AnimatorRootFolder + "/cat_action";
    private const string DogControllerFolder = AnimatorRootFolder + "/dog_action";
    private const string MinionControllerFolder = AnimatorRootFolder + "/minion_action";
    private const float RunStateSpeed = 2.5f;
    private const float AttackStateSpeed = 1.5f;
    private const float ActionStateSpeed = 3.5f;

    private CharacterKind characterKind = CharacterKind.Cat;
    private GameObject characterRoot;
    private AnimationClip idleClip;
    private AnimationClip runClip;
    private AnimationClip attackClip;
    private AnimationClip digClip;
    private AnimationClip buildClip;

    [MenuItem("Tools/Character Animator Setup")]
    public static void Open()
    {
        GetWindow<CharacterAnimatorControllerBuilder>("Character Animator Setup");
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
        EditorGUILayout.LabelField("Character Animator Setup", EditorStyles.boldLabel);
        bool isMinion = IsMinion(characterKind);
        EditorGUILayout.HelpBox(
            isMinion
                ? "Builds a 2-action controller for minions: Walk and Attack. Missing clips will get empty placeholder states so the controller can still be wired."
                : "Builds a 5-action controller for CAT or DOG: Idle, Run, Attack, Dig, and Build. Missing clips will get empty placeholder states so the controller can still be wired.",
            MessageType.Info);

        characterKind = (CharacterKind)EditorGUILayout.EnumPopup("Character Type", characterKind);
        characterRoot = (GameObject)EditorGUILayout.ObjectField("Character Root", characterRoot, typeof(GameObject), true);

        if (!isMinion)
        {
            idleClip = (AnimationClip)EditorGUILayout.ObjectField("Idle Clip", idleClip, typeof(AnimationClip), false);
        }

        runClip = (AnimationClip)EditorGUILayout.ObjectField(isMinion ? "Walk Clip" : "Run Clip", runClip, typeof(AnimationClip), false);
        attackClip = (AnimationClip)EditorGUILayout.ObjectField("Attack Clip", attackClip, typeof(AnimationClip), false);

        if (!isMinion)
        {
            digClip = (AnimationClip)EditorGUILayout.ObjectField("Dig Clip", digClip, typeof(AnimationClip), false);
            buildClip = (AnimationClip)EditorGUILayout.ObjectField("Build Clip", buildClip, typeof(AnimationClip), false);
        }

        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(characterRoot == null))
        {
            if (GUILayout.Button(isMinion ? "Build And Assign Minion Animator" : "Build And Assign 5 Action Animator", GUILayout.Height(32f)))
            {
                BuildAndAssign();
            }
        }
    }

    private void BuildAndAssign()
    {
        string controllerFolder = GetControllerFolder(characterKind);
        string controllerPath = GetControllerPath(characterKind);
        string characterPrefix = GetCharacterPrefix(characterKind);

        EnsureFolder(controllerFolder);

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        if (IsMinion(characterKind))
        {
            RebuildMinionController(controller, characterPrefix, controllerFolder);
        }
        else
        {
            RebuildCharacterController(controller, characterPrefix, controllerFolder);
        }

        Animator animator = characterRoot.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(characterRoot);
        }

        Undo.RecordObject(animator, "Assign Character Animator Controller");
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        if (!IsMinion(characterKind))
        {
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

            EditorUtility.SetDirty(playerController);
        }

        EditorUtility.SetDirty(animator);
        AssetDatabase.SaveAssets();

        Selection.activeObject = controller;
        EditorUtility.DisplayDialog("Character Animator Setup", "Done. " + controller.name + " was created and assigned.", "OK");
    }

    private void RebuildCharacterController(AnimatorController controller, string characterPrefix, string controllerFolder)
    {
        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dig", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Build", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState state in stateMachine.states)
        {
            stateMachine.RemoveState(state.state);
        }

        foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
        {
            stateMachine.RemoveStateMachine(childMachine.stateMachine);
        }

        AnimatorState idleState = CreateState(stateMachine, "Idle", idleClip, new Vector3(240f, 100f, 0f), true, characterPrefix, controllerFolder);
        AnimatorState runState = CreateState(stateMachine, "Run", runClip, new Vector3(480f, 100f, 0f), true, characterPrefix, controllerFolder);
        AnimatorState attackState = CreateState(stateMachine, "Attack", attackClip, new Vector3(360f, 260f, 0f), false, characterPrefix, controllerFolder);
        AnimatorState digState = CreateState(stateMachine, "Dig", digClip, new Vector3(560f, 260f, 0f), false, characterPrefix, controllerFolder);
        AnimatorState buildState = CreateState(stateMachine, "Build", buildClip, new Vector3(760f, 260f, 0f), false, characterPrefix, controllerFolder);

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

        EditorUtility.SetDirty(controller);
    }

    private void RebuildMinionController(AnimatorController controller, string characterPrefix, string controllerFolder)
    {
        controller.parameters = Array.Empty<AnimatorControllerParameter>();
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

        AnimatorState walkState = CreateState(stateMachine, "Walk", runClip, new Vector3(240f, 100f, 0f), true, characterPrefix, controllerFolder);
        AnimatorState attackState = CreateState(stateMachine, "Attack", attackClip, new Vector3(480f, 100f, 0f), false, characterPrefix, controllerFolder);

        attackState.speed = AttackStateSpeed;
        stateMachine.defaultState = walkState;

        AddActionTransition(stateMachine, attackState, "Attack");
        AddReturnTransition(attackState, walkState);

        EditorUtility.SetDirty(controller);
    }

    private static AnimatorState CreateState(AnimatorStateMachine stateMachine, string stateName, AnimationClip clip, Vector3 position, bool loop, string characterPrefix, string controllerFolder)
    {
        AnimatorState state = stateMachine.AddState(stateName, position);
        AnimationClip motion = clip != null ? clip : CreateEmptyClip(stateName, loop, characterPrefix, controllerFolder);
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

    private static AnimationClip CreateEmptyClip(string stateName, bool loop, string characterPrefix, string controllerFolder)
    {
        string clipName = characterPrefix + stateName + "Placeholder";
        string path = controllerFolder + "/" + clipName + ".anim";
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

    private static string GetControllerFolder(CharacterKind kind)
    {
        if (kind == CharacterKind.Cat)
        {
            return CatControllerFolder;
        }

        if (kind == CharacterKind.Dog)
        {
            return DogControllerFolder;
        }

        return MinionControllerFolder + "/" + GetCharacterPrefix(kind);
    }

    private static string GetControllerPath(CharacterKind kind)
    {
        return GetControllerFolder(kind) + "/" + GetCharacterPrefix(kind) + "Actions.controller";
    }

    private static string GetCharacterPrefix(CharacterKind kind)
    {
        switch (kind)
        {
            case CharacterKind.Cat:
                return "Cat";
            case CharacterKind.Dog:
                return "Dog";
            case CharacterKind.DogMeleeMinion:
                return "DogMeleeMinion";
            case CharacterKind.DogRangedMinion:
                return "DogRangedMinion";
            case CharacterKind.CatMeleeMinion:
                return "CatMeleeMinion";
            case CharacterKind.CatRangedMinion:
                return "CatRangedMinion";
            default:
                return "Character";
        }
    }

    private static bool IsMinion(CharacterKind kind)
    {
        return kind == CharacterKind.DogMeleeMinion
            || kind == CharacterKind.DogRangedMinion
            || kind == CharacterKind.CatMeleeMinion
            || kind == CharacterKind.CatRangedMinion;
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
}
