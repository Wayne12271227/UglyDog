using UnityEngine;
using UnityEngine.UI;

public class WorldSpaceHealthLabel : MonoBehaviour
{
    [SerializeField] private Vector3 offset = Vector3.up;
    [SerializeField] private Text text;
    [SerializeField] private Image healthFill;

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
                existingLabel.ApplyLayout(fontSize, size, scale);
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

    public static WorldSpaceHealthLabel CreateBaseHealthBar(
        Transform parent,
        string objectName,
        Vector3 labelOffset,
        MinionTeam team)
    {
        WorldSpaceHealthLabel label = Create(
            parent,
            objectName,
            labelOffset,
            44,
            new Vector2(360f, 94f),
            0.0085f);
        label.ConfigureBaseHealthBar(team);
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

    public void SetBaseHealth(MinionTeam team, int currentHealth, int maxHealth)
    {
        ConfigureBaseHealthBar(team);

        int safeMax = Mathf.Max(1, maxHealth);
        int safeCurrent = Mathf.Clamp(currentHealth, 0, safeMax);
        float normalized = Mathf.Clamp01((float)safeCurrent / safeMax);

        SetText(GetBaseHealthTitle(team) + "  " + safeCurrent + "/" + safeMax + " HP");
        if (healthFill != null)
        {
            healthFill.fillAmount = normalized;
            healthFill.color = GetTeamFillColor(team);
        }
    }

    private void ApplyLayout(int fontSize, Vector2 size, float scale)
    {
        transform.localScale = Vector3.one * scale;

        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = size;
        }

        if (text != null)
        {
            text.font = LoadReadableFont();
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
        }
    }

    private void ConfigureBaseHealthBar(MinionTeam team)
    {
        Image panel = GetOrAddImage(transform, "Base Health Panel");
        panel.color = new Color(0.12f, 0.08f, 0.045f, 0.86f);
        ConfigureRect(panel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));
        EnsureOutline(panel.gameObject, new Color(0.46f, 0.28f, 0.13f, 0.95f), new Vector2(2.5f, -2.5f));

        Image barBack = GetOrAddImage(transform, "Base Health Track");
        barBack.color = new Color(0.045f, 0.035f, 0.03f, 0.92f);
        ConfigureRect(barBack.rectTransform, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.42f), new Vector2(0f, 0f), new Vector2(0f, 0f));
        EnsureOutline(barBack.gameObject, new Color(0f, 0f, 0f, 0.7f), new Vector2(1.4f, -1.4f));

        healthFill = GetOrAddImage(barBack.transform, "Base Health Fill");
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        ConfigureRect(healthFill.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));

        Text uiText = text;
        if (uiText != null)
        {
            RectTransform textRect = uiText.GetComponent<RectTransform>();
            ConfigureRect(textRect, new Vector2(0.03f, 0.44f), new Vector2(0.97f, 0.98f), Vector2.zero, Vector2.zero);
            uiText.fontSize = 38;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.color = Color.white;
            uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
            uiText.verticalOverflow = VerticalWrapMode.Overflow;

            Outline textOutline = uiText.GetComponent<Outline>();
            if (textOutline != null)
            {
                textOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                textOutline.effectDistance = new Vector2(2.2f, -2.2f);
            }
        }

        transform.SetAsLastSibling();
        if (uiText != null)
        {
            uiText.transform.SetAsLastSibling();
        }
    }

    private static Image GetOrAddImage(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            childObject.transform.SetParent(parent, false);
            child = childObject.transform;
        }

        Image image = child.GetComponent<Image>();
        if (image == null)
        {
            image = child.gameObject.AddComponent<Image>();
        }

        image.raycastTarget = false;
        return image;
    }

    private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void EnsureOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.GetComponent<Outline>();
        if (outline == null)
        {
            outline = target.AddComponent<Outline>();
        }

        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    private static string GetBaseHealthTitle(MinionTeam team)
    {
        return team == MinionTeam.Dog ? "狗陣營" : "貓陣營";
    }

    private static Color GetTeamFillColor(MinionTeam team)
    {
        return team == MinionTeam.Dog
            ? new Color(0.95f, 0.55f, 0.22f, 1f)
            : new Color(0f, 0.847f, 1f, 1f);
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
