using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class DogToonAnimatorSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string DogAssetPath = "Assets/character/dog/tripo_convert_e08a885e-65db-4172-9653-8ec7d8e6fcf4.fbx";
    private const string DogTexturePath = "Assets/character/dog/tripo_convert_e08a885e-65db-4172-9653-8ec7d8e6fcf4.fbm/3dcartooncharactermodel_basecolor.JPEG";
    private const string ControllerFolder = "Assets/animator";
    private const string ControllerPath = ControllerFolder + "/DogActions.controller";
    private const string MaterialsFolder = "Assets/ToonURP/Materials";
    private const string DogToonMaterialPath = MaterialsFolder + "/DogToon.mat";
    private const string DogOutlineMaterialPath = MaterialsFolder + "/DogOutline.mat";
    private const string DogRootName = "DOG";
    private const string ToonShaderName = "Custom/ToonLitOutline";
    private const string OutlineShaderName = "Custom/URPToonOutline";

    private const float RunStateSpeed = 2.5f;
    private const float AttackStateSpeed = 1.5f;
    private const float ActionStateSpeed = 3.5f;

    static DogToonAnimatorSetup()
    {
        EditorApplication.delayCall += SetupDogAutomatically;
    }

    [MenuItem("Tools/Setup Dog Toon And Animator")]
    public static void SetupDogFromMenu()
    {
        SetupDogWhenReady(true);
    }

    private static void SetupDogAutomatically()
    {
        SetupDogWhenReady(false);
    }

    private static void SetupDogWhenReady(bool allowOpenScene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += SetupDogAutomatically;
            return;
        }

        if (!File.Exists(ScenePath) || AssetDatabase.LoadAssetAtPath<GameObject>(DogAssetPath) == null)
        {
            return;
        }

        bool openedByTool = false;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded && !allowOpenScene)
        {
            return;
        }

        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            openedByTool = true;
        }

        GameObject dogRoot = FindDogRoot(scene);
        if (dogRoot == null)
        {
            dogRoot = CreateDogInstance(scene);
        }

        if (dogRoot == null)
        {
            return;
        }

        Material toonMaterial = EnsureDogToonMaterial();
        Material outlineMaterial = EnsureDogOutlineMaterial();
        ApplyToonSetup(dogRoot, toonMaterial, outlineMaterial);
        AssignAnimator(dogRoot);

        EditorUtility.SetDirty(dogRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        if (openedByTool)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static GameObject FindDogRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == DogRootName || root.name.Contains("tripo_convert_e08a885e"))
            {
                return root;
            }
        }

        return null;
    }

    private static GameObject CreateDogInstance(Scene scene)
    {
        GameObject dogAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DogAssetPath);
        if (dogAsset == null)
        {
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(dogAsset, scene) as GameObject;
        if (instance == null)
        {
            instance = UnityEngine.Object.Instantiate(dogAsset);
            SceneManager.MoveGameObjectToScene(instance, scene);
        }

        instance.name = DogRootName;
        instance.transform.position = new Vector3(1.6f, 0f, -1.3f);
        instance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        instance.transform.localScale = Vector3.one;
        return instance;
    }

    private static Material EnsureDogToonMaterial()
    {
        EnsureFolder("Assets/ToonURP");
        EnsureFolder(MaterialsFolder);

        Shader toonShader = Shader.Find(ToonShaderName);
        if (toonShader == null)
        {
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(DogToonMaterialPath);
        if (material == null)
        {
            material = new Material(toonShader);
            AssetDatabase.CreateAsset(material, DogToonMaterialPath);
        }

        material.shader = toonShader;
        Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(DogTexturePath);
        if (texture != null && material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        SetDogToonValues(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material EnsureDogOutlineMaterial()
    {
        EnsureFolder("Assets/ToonURP");
        EnsureFolder(MaterialsFolder);

        Shader outlineShader = Shader.Find(OutlineShaderName);
        if (outlineShader == null)
        {
            outlineShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/URPToonOutline.shader");
        }

        if (outlineShader == null)
        {
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(DogOutlineMaterialPath);
        if (material == null)
        {
            material = new Material(outlineShader);
            AssetDatabase.CreateAsset(material, DogOutlineMaterialPath);
        }

        material.shader = outlineShader;
        if (material.HasProperty("_OutlineColor"))
        {
            material.SetColor("_OutlineColor", new Color(0.12f, 0.07f, 0.05f, 1f));
        }

        if (material.HasProperty("_OutlineWidth"))
        {
            material.SetFloat("_OutlineWidth", 0.012f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ApplyToonSetup(GameObject dogRoot, Material toonMaterial, Material outlineMaterial)
    {
        ToonCharacterSetup setup = dogRoot.GetComponent<ToonCharacterSetup>();
        if (setup == null)
        {
            setup = Undo.AddComponent<ToonCharacterSetup>(dogRoot);
        }

        SerializedObject serializedSetup = new SerializedObject(setup);
        serializedSetup.FindProperty("targetRootName").stringValue = dogRoot.name;
        serializedSetup.FindProperty("targetRoot").objectReferenceValue = dogRoot.transform;
        serializedSetup.FindProperty("baseToonMaterial").objectReferenceValue = toonMaterial;
        serializedSetup.FindProperty("toonShaderName").stringValue = ToonShaderName;
        serializedSetup.FindProperty("outlineMaterial").objectReferenceValue = outlineMaterial;
        serializedSetup.FindProperty("enableOutline").boolValue = true;
        serializedSetup.FindProperty("preserveExistingMaterialTextures").boolValue = false;
        serializedSetup.FindProperty("baseColor").colorValue = Color.white;
        serializedSetup.FindProperty("shadowColor").colorValue = new Color(0.64f, 0.48f, 0.38f, 1f);
        serializedSetup.FindProperty("shadowThreshold").floatValue = 0.42f;
        serializedSetup.FindProperty("shadowSmoothness").floatValue = 0.04f;
        serializedSetup.FindProperty("rimColor").colorValue = new Color(1f, 0.88f, 0.68f, 1f);
        serializedSetup.FindProperty("rimPower").floatValue = 3.5f;
        serializedSetup.FindProperty("rimStrength").floatValue = 0.28f;
        serializedSetup.FindProperty("outlineColor").colorValue = new Color(0.12f, 0.07f, 0.05f, 1f);
        serializedSetup.FindProperty("outlineWidth").floatValue = 0.012f;
        serializedSetup.ApplyModifiedPropertiesWithoutUndo();

        setup.ApplyToonStyle();
        EditorUtility.SetDirty(setup);
    }

    private static void AssignAnimator(GameObject dogRoot)
    {
        Directory.CreateDirectory(ControllerFolder);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        RebuildController(controller);

        Animator animator = dogRoot.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(dogRoot);
        }

        Undo.RecordObject(animator, "Assign Dog Animator Controller");
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        CatPlayerController playerController = dogRoot.GetComponent<CatPlayerController>();
        if (playerController == null)
        {
            playerController = Undo.AddComponent<CatPlayerController>(dogRoot);
        }

        SerializedObject serializedPlayer = new SerializedObject(playerController);
        serializedPlayer.FindProperty("animator").objectReferenceValue = animator;
        serializedPlayer.FindProperty("speedParameter").stringValue = "Speed";
        serializedPlayer.FindProperty("attackTrigger").stringValue = "Attack";
        serializedPlayer.FindProperty("digTrigger").stringValue = "Dig";
        serializedPlayer.FindProperty("buildTrigger").stringValue = "Build";
        serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(playerController);
    }

    private static void RebuildController(AnimatorController controller)
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

        AnimationClip idleClip = FindClip("Idle");
        AnimationClip runClip = FindClip("Run");
        AnimationClip attackClip = FindClip("Attack");
        AnimationClip digClip = FindClip("Dig");
        AnimationClip buildClip = FindClip("Build");

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

        EditorUtility.SetDirty(controller);
    }

    private static AnimationClip FindClip(string actionName)
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(DogAssetPath)
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        AnimationClip exact = clips.FirstOrDefault(clip => string.Equals(clip.name, actionName, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
        {
            return exact;
        }

        return clips.FirstOrDefault(clip => clip.name.IndexOf(actionName, StringComparison.OrdinalIgnoreCase) >= 0);
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
        string path = ControllerFolder + "/Dog" + stateName + "Placeholder.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip
            {
                name = "Dog" + stateName + "Placeholder",
                frameRate = 30f
            };
            AssetDatabase.CreateAsset(clip, path);
        }

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

    private static void SetDogToonValues(Material material)
    {
        SetColorIfAvailable(material, "_Color", Color.white);
        SetColorIfAvailable(material, "_ShadowColor", new Color(0.64f, 0.48f, 0.38f, 1f));
        SetFloatIfAvailable(material, "_ShadowThreshold", 0.42f);
        SetFloatIfAvailable(material, "_ShadowSmoothness", 0.04f);
        SetColorIfAvailable(material, "_RimColor", new Color(1f, 0.88f, 0.68f, 1f));
        SetFloatIfAvailable(material, "_RimPower", 3.5f);
        SetFloatIfAvailable(material, "_RimStrength", 0.28f);
        SetColorIfAvailable(material, "_OutlineColor", new Color(0.12f, 0.07f, 0.05f, 1f));
        SetFloatIfAvailable(material, "_OutlineWidth", 0.012f);
    }

    private static void SetColorIfAvailable(Material material, string propertyName, Color value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static void SetFloatIfAvailable(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
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
}
