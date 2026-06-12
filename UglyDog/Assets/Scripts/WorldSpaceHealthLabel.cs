using UnityEngine;
using UnityEngine.UI;

public class WorldSpaceHealthLabel : MonoBehaviour
{
    [SerializeField] private Vector3 offset = Vector3.up;
    [SerializeField] private Text text;

    public static WorldSpaceHealthLabel Create(
        Transform parent,
        string objectName,
        Vector3 labelOffset,
        int fontSize,
        Vector2 size,
        float scale)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            WorldSpaceHealthLabel existingLabel = existing.GetComponent<WorldSpaceHealthLabel>();
            if (existingLabel != null)
            {
                existingLabel.offset = labelOffset;
                existingLabel.SetText(string.Empty);
                return existingLabel;
            }

            if (Application.isPlaying)
            {
                Destroy(existing.gameObject);
            }
            else
            {
                DestroyImmediate(existing.gameObject);
            }
        }
        else
        {
            GameObject legacyObject = GameObject.Find(objectName);
            if (legacyObject != null && legacyObject.GetComponent<WorldSpaceHealthLabel>() == null)
            {
                if (Application.isPlaying)
                {
                    Destroy(legacyObject);
                }
                else
                {
                    DestroyImmediate(legacyObject);
                }
            }
        }

        GameObject root = new GameObject(objectName, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        root.transform.localPosition = labelOffset;
        root.transform.localScale = Vector3.one * scale;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = size;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(root.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text uiText = textObject.AddComponent<Text>();
        uiText.font = LoadReadableFont();
        uiText.alignment = TextAnchor.MiddleCenter;
        uiText.fontSize = fontSize;
        uiText.fontStyle = FontStyle.Bold;
        uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
        uiText.verticalOverflow = VerticalWrapMode.Overflow;
        uiText.color = Color.white;
        uiText.raycastTarget = false;

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        WorldSpaceHealthLabel label = root.AddComponent<WorldSpaceHealthLabel>();
        label.offset = labelOffset;
        label.text = uiText;
        return label;
    }

    public void AttachTo(Transform parent, Vector3 labelOffset)
    {
        transform.SetParent(parent, false);
        offset = labelOffset;
        transform.localPosition = offset;
    }

    private static Font LoadReadableFont()
    {
        return UglyDogUIFont.Load();
    }

    public void SetText(string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private void LateUpdate()
    {
        transform.localPosition = offset;

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.worldCamera == null)
        {
            canvas.worldCamera = camera;
        }

        Vector3 direction = transform.position - camera.transform.position;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}
