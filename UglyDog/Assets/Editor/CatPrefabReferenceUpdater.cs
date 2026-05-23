using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CatPrefabReferenceUpdater
{
    private const string OldCatGuid = "f20693ef168a0aa46a5cd7fdf2e42956";
    private const string OldCatPath = "Assets/prefab/character/CAT.prefab";
    private const string NewCatPath = "Assets/prefab/character/CAT2 1.prefab";

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/RoomLobby.unity",
        "Assets/Scenes/AnimatorTest.unity"
    };

    static CatPrefabReferenceUpdater()
    {
        EditorApplication.delayCall += ReplaceOldCatReferencesIfNeeded;
    }

    [MenuItem("Tools/UglyDog/Replace CAT Prefab References")]
    public static void ReplaceOldCatReferencesIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += ReplaceOldCatReferencesIfNeeded;
            return;
        }

        GameObject newCatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NewCatPath);
        if (newCatPrefab == null)
        {
            Debug.LogWarning("Could not load " + NewCatPath + ".");
            return;
        }

        MethodInfo replaceMethod = FindReplacePrefabMethod();
        if (replaceMethod == null)
        {
            Debug.LogWarning("Could not find PrefabUtility.ReplacePrefabAssetOfPrefabInstance.");
            return;
        }

        string activeScenePath = SceneManager.GetActiveScene().path;
        bool replacedAny = false;

        for (int i = 0; i < ScenePaths.Length; i++)
        {
            string scenePath = ScenePaths[i];
            if (!SceneContainsOldCatGuid(scenePath))
            {
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool changed = ReplaceOldCatInstancesInScene(scene, newCatPrefab, replaceMethod);
            if (changed)
            {
                EditorSceneManager.SaveScene(scene);
                replacedAny = true;
            }
        }

        if (!string.IsNullOrEmpty(activeScenePath) && File.Exists(activeScenePath))
        {
            EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
        }

        if (replacedAny)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static bool SceneContainsOldCatGuid(string scenePath)
    {
        if (!File.Exists(scenePath))
        {
            return false;
        }

        return File.ReadAllText(scenePath).Contains(OldCatGuid);
    }

    private static bool ReplaceOldCatInstancesInScene(Scene scene, GameObject newCatPrefab, MethodInfo replaceMethod)
    {
        bool changed = false;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                GameObject candidate = transforms[j].gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(candidate))
                {
                    continue;
                }

                UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(candidate);
                if (source == null || AssetDatabase.GetAssetPath(source) != OldCatPath)
                {
                    continue;
                }

                InvokeReplacePrefab(replaceMethod, candidate, newCatPrefab);
                changed = true;
            }
        }

        return changed;
    }

    private static MethodInfo FindReplacePrefabMethod()
    {
        MethodInfo[] methods = typeof(PrefabUtility).GetMethods(BindingFlags.Public | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name != "ReplacePrefabAssetOfPrefabInstance")
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length >= 3
                && parameters[0].ParameterType == typeof(GameObject)
                && parameters[1].ParameterType == typeof(GameObject))
            {
                return method;
            }
        }

        return null;
    }

    private static void InvokeReplacePrefab(MethodInfo replaceMethod, GameObject instanceRoot, GameObject newPrefab)
    {
        ParameterInfo[] parameters = replaceMethod.GetParameters();
        object[] arguments = new object[parameters.Length];
        arguments[0] = instanceRoot;
        arguments[1] = newPrefab;

        for (int i = 2; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            if (parameterType == typeof(InteractionMode))
            {
                arguments[i] = InteractionMode.AutomatedAction;
            }
            else
            {
                arguments[i] = Activator.CreateInstance(parameterType);
            }
        }

        replaceMethod.Invoke(null, arguments);
    }
}
