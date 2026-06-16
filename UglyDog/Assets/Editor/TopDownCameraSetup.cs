using UnityEditor;
using UnityEngine;
using System.IO;

public static class TopDownCameraSetup
{
    private const string AutoFixRequestPath = "Temp/FixSampleSceneCamera.request";

    [InitializeOnLoadMethod]
    private static void AutoFixWhenRequested()
    {
        if (!File.Exists(AutoFixRequestPath))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(AutoFixRequestPath))
            {
                return;
            }

            File.Delete(AutoFixRequestPath);
            KeepOnlySampleSceneCamera();
        };
    }

    [MenuItem("Tools/Setup Top Down Camera")]
    public static void Setup()
    {
        KeepOnlySampleSceneCamera();

        Camera camera = Camera.main;
        if (camera == null)
        {
            EditorUtility.DisplayDialog("Setup Top Down Camera", "No Main Camera found in the scene.", "OK");
            return;
        }

        CatPlayerController player = Object.FindObjectOfType<CatPlayerController>();
        if (player == null)
        {
            EditorUtility.DisplayDialog("Setup Top Down Camera", "No CatPlayerController found. Select CAT and make sure CatPlayerController is attached.", "OK");
            return;
        }

        TopDownCameraFollow follow = camera.GetComponent<TopDownCameraFollow>();
        if (follow == null)
        {
            follow = Undo.AddComponent<TopDownCameraFollow>(camera.gameObject);
        }

        Undo.RecordObject(follow, "Setup Top Down Camera");
        follow.Target = player.transform;
        follow.SnapToTarget();

        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(follow);
        Selection.activeGameObject = camera.gameObject;

        EditorUtility.DisplayDialog("Setup Top Down Camera", "Done. Main Camera now follows CAT with the SampleScene camera view.", "OK");
    }

    [MenuItem("Tools/Fix SampleScene Camera")]
    public static void FixSampleSceneCamera()
    {
        KeepOnlySampleSceneCamera();

        Camera camera = Camera.main;
        if (camera == null)
        {
            EditorUtility.DisplayDialog("Fix SampleScene Camera", "No Main Camera found in the scene.", "OK");
            return;
        }

        TopDownCameraFollow follow = camera.GetComponent<TopDownCameraFollow>();
        if (follow == null)
        {
            follow = Undo.AddComponent<TopDownCameraFollow>(camera.gameObject);
        }

        Undo.RecordObject(camera, "Fix SampleScene Camera");
        Undo.RecordObject(follow, "Fix SampleScene Camera");
        follow.SnapToTarget();

        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(follow);
        Selection.activeGameObject = camera.gameObject;

        EditorUtility.DisplayDialog("Fix SampleScene Camera", "Done. Only SampleScene Main Camera is active.", "OK");
    }

    private static void KeepOnlySampleSceneCamera()
    {
        Camera sampleCamera = Camera.main;
        if (sampleCamera == null)
        {
            Camera[] sceneCameras = Object.FindObjectsOfType<Camera>(true);
            foreach (Camera camera in sceneCameras)
            {
                if (camera.name == "Main Camera")
                {
                    sampleCamera = camera;
                    break;
                }
            }
        }

        if (sampleCamera == null)
        {
            return;
        }

        Undo.RecordObject(sampleCamera.gameObject, "Keep SampleScene Camera");
        sampleCamera.gameObject.SetActive(true);
        sampleCamera.tag = "MainCamera";

        Camera[] cameras = Object.FindObjectsOfType<Camera>(true);
        foreach (Camera camera in cameras)
        {
            if (camera == sampleCamera)
            {
                continue;
            }

            Undo.RecordObject(camera.gameObject, "Disable Extra Camera");
            camera.gameObject.SetActive(false);
            if (camera.CompareTag("MainCamera"))
            {
                camera.tag = "Untagged";
            }
        }
    }
}
