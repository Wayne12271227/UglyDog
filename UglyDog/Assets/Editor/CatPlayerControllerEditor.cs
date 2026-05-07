using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CatPlayerController))]
public class CatPlayerControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CatPlayerController player = (CatPlayerController)target;
        CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.HelpBox("Add a Capsule Collider to this object if you want a manually adjustable player collider.", MessageType.Info);

            if (GUILayout.Button("Add Capsule Collider"))
            {
                capsule = Undo.AddComponent<CapsuleCollider>(player.gameObject);
                ResetCapsule(player, capsule);
            }

            return;
        }

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Capsule Collider Manual Adjust", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        Vector3 center = EditorGUILayout.Vector3Field("Center", capsule.center);
        float radius = EditorGUILayout.FloatField("Radius", capsule.radius);
        float height = EditorGUILayout.FloatField("Height", capsule.height);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(capsule, "Adjust Capsule Collider");
            capsule.center = center;
            capsule.radius = Mathf.Max(0.01f, radius);
            capsule.height = Mathf.Max(capsule.radius * 2f, height);
            capsule.direction = 1;
            EditorUtility.SetDirty(capsule);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Fit To Cat Mesh"))
        {
            FitCapsuleToRenderers(player, capsule);
        }

        if (GUILayout.Button("Align Feet"))
        {
            AlignCapsuleBottomToFeet(player, capsule);
        }

        if (GUILayout.Button("Reset Upright"))
        {
            ResetCapsule(player, capsule);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox("You can also drag the green handles in the Scene view: middle = center, top/bottom = height, circle = radius.", MessageType.Info);
    }

    private void OnSceneGUI()
    {
        CatPlayerController player = (CatPlayerController)target;
        CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            return;
        }

        Transform transform = player.transform;
        Vector3 worldCenter = transform.TransformPoint(capsule.center);
        Vector3 worldUp = transform.up;
        float scale = GetAverageScale(transform);
        float radius = capsule.radius * scale;
        float height = Mathf.Max(capsule.height * scale, radius * 2f);

        Handles.color = new Color(0.15f, 0.9f, 0.55f, 0.9f);
        Vector3 newWorldCenter = Handles.PositionHandle(worldCenter, transform.rotation);
        if (newWorldCenter != worldCenter)
        {
            Undo.RecordObject(capsule, "Move Capsule Collider Center");
            capsule.center = transform.InverseTransformPoint(newWorldCenter);
            EditorUtility.SetDirty(capsule);
        }

        worldCenter = transform.TransformPoint(capsule.center);
        height = Mathf.Max(capsule.height * scale, radius * 2f);

        Vector3 top = worldCenter + worldUp * (height * 0.5f);
        Vector3 bottom = worldCenter - worldUp * (height * 0.5f);

        EditorGUI.BeginChangeCheck();
        Vector3 newTop = Handles.Slider(top, worldUp, HandleUtility.GetHandleSize(top) * 0.12f, Handles.SphereHandleCap, 0f);
        Vector3 newBottom = Handles.Slider(bottom, -worldUp, HandleUtility.GetHandleSize(bottom) * 0.12f, Handles.SphereHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(capsule, "Resize Capsule Collider Height");

            float topDistance = Vector3.Dot(newTop - worldCenter, worldUp);
            float bottomDistance = Vector3.Dot(worldCenter - newBottom, worldUp);
            float newHeight = Mathf.Max(topDistance + bottomDistance, capsule.radius * 2f * scale) / scale;
            Vector3 centerOffset = worldUp * ((topDistance - bottomDistance) * 0.5f);

            capsule.height = newHeight;
            capsule.center = transform.InverseTransformPoint(worldCenter + centerOffset);
            capsule.direction = 1;
            EditorUtility.SetDirty(capsule);
        }

        EditorGUI.BeginChangeCheck();
        float newRadius = Handles.RadiusHandle(transform.rotation, worldCenter, radius);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(capsule, "Resize Capsule Collider Radius");
            capsule.radius = Mathf.Max(0.01f, newRadius / scale);
            capsule.height = Mathf.Max(capsule.height, capsule.radius * 2f);
            capsule.direction = 1;
            EditorUtility.SetDirty(capsule);
        }
    }

    private static float GetAverageScale(Transform transform)
    {
        Vector3 scale = transform.lossyScale;
        return (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f;
    }

    private static void FitCapsuleToRenderers(CatPlayerController player, CapsuleCollider capsule)
    {
        Renderer[] renderers = player.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Transform transform = player.transform;
        Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
        Vector3 lossyScale = transform.lossyScale;
        float scaleX = Mathf.Max(0.001f, Mathf.Abs(lossyScale.x));
        float scaleY = Mathf.Max(0.001f, Mathf.Abs(lossyScale.y));
        float scaleZ = Mathf.Max(0.001f, Mathf.Abs(lossyScale.z));

        float height = bounds.size.y / scaleY;
        float radius = Mathf.Max(bounds.size.x / scaleX, bounds.size.z / scaleZ) * 0.5f;

        Undo.RecordObject(capsule, "Fit Capsule Collider To Cat Mesh");
        capsule.center = localCenter;
        capsule.radius = Mathf.Max(0.05f, radius * 0.45f);
        capsule.height = Mathf.Max(capsule.radius * 2f, height * 0.9f);
        capsule.direction = 1;
        EditorUtility.SetDirty(capsule);
        SceneView.RepaintAll();
    }

    private static void AlignCapsuleBottomToFeet(CatPlayerController player, CapsuleCollider capsule)
    {
        Renderer[] renderers = player.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Transform transform = player.transform;
        Vector3 footWorldPoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        float footLocalY = transform.InverseTransformPoint(footWorldPoint).y;

        Undo.RecordObject(capsule, "Align Capsule Collider Feet");
        capsule.center = new Vector3(capsule.center.x, footLocalY + capsule.height * 0.5f, capsule.center.z);
        capsule.direction = 1;
        EditorUtility.SetDirty(capsule);
        SceneView.RepaintAll();
    }

    private static void ResetCapsule(CatPlayerController player, CapsuleCollider capsule)
    {
        Undo.RecordObject(capsule, "Reset Capsule Collider");
        capsule.direction = 1;
        capsule.radius = 0.35f;
        capsule.height = 1.4f;
        capsule.center = new Vector3(0f, 0.7f, 0f);
        EditorUtility.SetDirty(capsule);
        SceneView.RepaintAll();
    }
}
