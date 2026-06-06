using UnityEngine;

public class DamagePopup : MonoBehaviour
{
    private const float DefaultLifetime = 0.75f;
    private const float DefaultRiseSpeed = 1.15f;

    private TextMesh textMesh;
    private Color startColor;
    private float lifetime;
    private float age;
    private Vector3 startScale;

    public static DamagePopup Spawn(Vector3 position, string text)
    {
        GameObject popupObject = new GameObject("Damage Popup");
        popupObject.transform.position = position;

        TextMesh mesh = popupObject.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.fontSize = 72;
        mesh.characterSize = 0.045f;
        mesh.color = new Color(1f, 0.18f, 0.08f, 1f);

        MeshRenderer renderer = popupObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 100;
        }

        DamagePopup popup = popupObject.AddComponent<DamagePopup>();
        popup.Initialize(mesh, DefaultLifetime);
        return popup;
    }

    private void Initialize(TextMesh mesh, float popupLifetime)
    {
        textMesh = mesh;
        lifetime = Mathf.Max(0.05f, popupLifetime);
        startColor = textMesh.color;
        startScale = transform.localScale;
        FaceCamera();
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(age / lifetime);

        transform.position += Vector3.up * DefaultRiseSpeed * Time.unscaledDeltaTime;
        transform.localScale = startScale * Mathf.Lerp(1f, 1.35f, t);
        FaceCamera();

        Color color = startColor;
        color.a = 1f - t;
        textMesh.color = color;

        if (age >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void FaceCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, Vector3.up);
    }
}
