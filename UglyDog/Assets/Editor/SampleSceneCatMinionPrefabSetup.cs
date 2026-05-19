using System.IO;
using UnityEditor.Animations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SampleSceneCatMinionPrefabSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RequestPath = "Temp/SetupSampleSceneCatMinionPrefabs.request";
    private const string CatMeleePrefabPath = "Assets/prefab/cat_melee.prefab";
    private const string CatRangedPrefabPath = "Assets/prefab/cat_ranged.prefab";
    private const string CatMeleeModelPath = "Assets/low_poly_model/minion/cat_minion01/tripo_convert_3492f0fb-46be-4020-96b6-094a94d82626.fbx";
    private const string CatRangedModelPath = "Assets/low_poly_model/minion/cat_minion02/tripo_convert_741097ac-70c2-41ba-b2ca-ceb4316fb90c.fbx";

    [InitializeOnLoadMethod]
    private static void AutoSetupWhenRequested()
    {
        if (!File.Exists(RequestPath))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(RequestPath))
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update += SetupWhenEditorLeavesPlayMode;
                return;
            }

            File.Delete(RequestPath);
            SetupSampleScene();
        };
    }

    private static void SetupWhenEditorLeavesPlayMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EditorApplication.update -= SetupWhenEditorLeavesPlayMode;
        if (File.Exists(RequestPath))
        {
            File.Delete(RequestPath);
            SetupSampleScene();
        }
    }

    [MenuItem("Tools/Minions/Setup Sample Scene Cat Minion Prefabs")]
    public static void SetupSampleScene()
    {
        EditorSceneManager.OpenScene(ScenePath);

        GameObject catMeleePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CatMeleePrefabPath);
        GameObject catRangedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CatRangedPrefabPath);
        if (catMeleePrefab == null || catRangedPrefab == null)
        {
            Debug.LogError("Missing cat minion prefab. Expected cat_melee and cat_ranged under Assets/prefab.");
            return;
        }

        EnsurePrefabAnimatorAvatar(CatMeleePrefabPath, CatMeleeModelPath);
        EnsurePrefabAnimatorAvatar(CatRangedPrefabPath, CatRangedModelPath);
        EnsurePrefabControllerClips(CatMeleePrefabPath);
        EnsurePrefabControllerClips(CatRangedPrefabPath);

        MinionManager manager = Object.FindObjectOfType<MinionManager>();
        if (manager == null)
        {
            GameObject managerObject = new GameObject("Minion Manager");
            manager = managerObject.AddComponent<MinionManager>();
        }
        else
        {
            manager.gameObject.name = "Minion Manager";
        }

        SerializedObject serializedManager = new SerializedObject(manager);
        serializedManager.FindProperty("catMeleeVisualPrefab").objectReferenceValue = catMeleePrefab;
        serializedManager.FindProperty("catRangedVisualPrefab").objectReferenceValue = catRangedPrefab;
        serializedManager.FindProperty("catMeleeVisualYawOffset").floatValue = 180f;
        serializedManager.FindProperty("catRangedVisualYawOffset").floatValue = 180f;
        serializedManager.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Selection.activeObject = manager.gameObject;
        Debug.Log("SampleScene now uses cat_melee and cat_ranged for cat minion visuals.");
    }

    private static void EnsurePrefabAnimatorAvatar(string prefabPath, string modelPath)
    {
        Avatar avatar = LoadFirstAvatar(modelPath);
        if (avatar == null)
        {
            Debug.LogWarning("No Avatar found in " + modelPath + ". Animator may not play skeletal clips.");
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Animator animator = prefabRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogWarning("No Animator found in " + prefabPath);
                return;
            }

            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(animator);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static Avatar LoadFirstAvatar(string modelPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
        for (int i = 0; i < assets.Length; i++)
        {
            Avatar avatar = assets[i] as Avatar;
            if (avatar != null)
            {
                return avatar;
            }
        }

        return null;
    }

    private static void EnsurePrefabControllerClips(string prefabPath)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Animator animator = prefabRoot.GetComponentInChildren<Animator>(true);
            AnimatorController controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
            if (controller == null)
            {
                return;
            }

            bool changed = false;
            ChildAnimatorState[] states = controller.layers.Length > 0 ? controller.layers[0].stateMachine.states : new ChildAnimatorState[0];
            for (int i = 0; i < states.Length; i++)
            {
                AnimatorState state = states[i].state;
                AnimationClip clip = state != null ? state.motion as AnimationClip : null;
                if (clip == null)
                {
                    continue;
                }

                if (state.name == "Walk")
                {
                    changed |= SetLoopTime(clip, true);
                }
                else if (state.name == "Attack")
                {
                    changed |= SetLoopTime(clip, false);
                }
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool SetLoopTime(AnimationClip clip, bool loop)
    {
        SerializedObject serializedClip = new SerializedObject(clip);
        SerializedProperty settings = serializedClip.FindProperty("m_AnimationClipSettings");
        if (settings == null)
        {
            return false;
        }

        SerializedProperty loopTime = settings.FindPropertyRelative("m_LoopTime");
        SerializedProperty loopBlend = settings.FindPropertyRelative("m_LoopBlend");
        bool changed = false;

        if (loopTime != null && loopTime.boolValue != loop)
        {
            loopTime.boolValue = loop;
            changed = true;
        }

        if (loopBlend != null && loopBlend.boolValue != loop)
        {
            loopBlend.boolValue = loop;
            changed = true;
        }

        if (changed)
        {
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clip);
        }

        return changed;
    }
}
