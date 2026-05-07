using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class ResourceHudUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ResourceManager resourceManager;

    [Header("Layout")]
    [SerializeField] private Vector2 topRightOffset = new Vector2(-24f, -18f);
    [SerializeField] private Vector2 itemSize = new Vector2(150f, 54f);
    [SerializeField] private float itemSpacing = 10f;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.68f);
    [SerializeField] private Color iconPlaceholderColor = new Color(1f, 1f, 1f, 0.35f);

    private Text coinText;
    private Text woodText;
    private Text stoneText;
    private ResourceManager subscribedManager;
    private int lastCoins = int.MinValue;
    private int lastWood = int.MinValue;
    private int lastStone = int.MinValue;
    private bool editorRebuildQueued;

    private void Awake()
    {
        BuildUI();
    }

    private void OnEnable()
    {
        ApplyReadableDefaults();
        BuildUI();
        TrySubscribe();
        Refresh();
    }

    private void OnValidate()
    {
        ApplyReadableDefaults();

        if (isActiveAndEnabled)
        {
            QueueEditorRebuild();
        }
    }

    private void QueueEditorRebuild()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || editorRebuildQueued)
        {
            return;
        }

        editorRebuildQueued = true;
        UnityEditor.EditorApplication.delayCall += RebuildInEditor;
#endif
    }

    private void RebuildInEditor()
    {
#if UNITY_EDITOR
        editorRebuildQueued = false;
        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        BuildUI();
        Refresh();
#endif
    }

    private void OnDisable()
    {
        if (subscribedManager != null)
        {
            subscribedManager.ResourcesChanged -= Refresh;
            subscribedManager = null;
        }
    }

    private void Update()
    {
        TrySubscribe();
        BindValueTexts();
        Refresh();
    }

    public void Refresh()
    {
        ResourceManager manager = GetResourceManager();
        if (manager == null)
        {
            SetText(coinText, "0");
            SetText(woodText, "0");
            SetText(stoneText, "0");
            return;
        }

        int coins = manager.Coins;
        int wood = manager.Wood;
        int stone = manager.Stone;

        string coinValue = coins.ToString();
        string woodValue = wood.ToString();
        string stoneValue = stone.ToString();

        bool valuesChanged = coins != lastCoins
            || wood != lastWood
            || stone != lastStone
            || coinText == null
            || woodText == null
            || stoneText == null
            || coinText.text != coinValue
            || woodText.text != woodValue
            || stoneText.text != stoneValue;

        if (!valuesChanged)
        {
            return;
        }

        lastCoins = coins;
        lastWood = wood;
        lastStone = stone;

        SetText(coinText, coinValue);
        SetText(woodText, woodValue);
        SetText(stoneText, stoneValue);
    }

    private void BuildUI()
    {
        RectTransform root = GetComponent<RectTransform>();
        root.anchorMin = new Vector2(1f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = topRightOffset;
        root.sizeDelta = new Vector2(itemSize.x * 3f + itemSpacing * 2f, itemSize.y);

        ClearChildren();

        coinText = CreateResourceItem("Coin Slot", "\u91d1\u5e63", 0);
        woodText = CreateResourceItem("Wood Slot", "\u6728\u982d", 1);
        stoneText = CreateResourceItem("Stone Slot", "\u77f3\u982d", 2);
        BindValueTexts();
        ForceNextRefresh();
    }

    private void TrySubscribe()
    {
        ResourceManager manager = GetResourceManager();
        if (subscribedManager == manager)
        {
            return;
        }

        if (subscribedManager != null)
        {
            subscribedManager.ResourcesChanged -= Refresh;
        }

        subscribedManager = manager;
        if (subscribedManager != null)
        {
            subscribedManager.ResourcesChanged += Refresh;
            Refresh();
        }
    }

    private ResourceManager GetResourceManager()
    {
        if (resourceManager != null && resourceManager.isActiveAndEnabled)
        {
            return resourceManager;
        }

        if (ResourceManager.Instance != null)
        {
            resourceManager = ResourceManager.Instance;
            return resourceManager;
        }

        ResourceManager[] managers = FindObjectsOfType<ResourceManager>(true);
        foreach (ResourceManager manager in managers)
        {
            if (manager.gameObject.name == "ResourceManager" && manager.isActiveAndEnabled)
            {
                resourceManager = manager;
                return resourceManager;
            }
        }

        foreach (ResourceManager manager in managers)
        {
            if (manager.isActiveAndEnabled)
            {
                resourceManager = manager;
                return resourceManager;
            }
        }

        return resourceManager;
    }

    private void BindValueTexts()
    {
        coinText = FindValueText("Coin Slot");
        woodText = FindValueText("Wood Slot");
        stoneText = FindValueText("Stone Slot");
    }

    private Text FindValueText(string slotName)
    {
        Transform slot = transform.Find(slotName);
        if (slot == null)
        {
            return null;
        }

        Transform value = slot.Find("Value");
        if (value == null)
        {
            return null;
        }

        return value.GetComponent<Text>();
    }

    private Text CreateResourceItem(string itemName, string resourceName, int index)
    {
        GameObject item = new GameObject(itemName, typeof(RectTransform), typeof(Image));
        item.transform.SetParent(transform, false);

        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(1f, 0.5f);
        itemRect.anchorMax = new Vector2(1f, 0.5f);
        itemRect.pivot = new Vector2(1f, 0.5f);
        itemRect.sizeDelta = itemSize;
        itemRect.anchoredPosition = new Vector2(-(itemSize.x + itemSpacing) * (2 - index), 0f);

        Image background = item.GetComponent<Image>();
        background.color = backgroundColor;
        background.raycastTarget = false;

        GameObject icon = new GameObject("Icon Placeholder", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(item.transform, false);

        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(10f, 0f);
        iconRect.sizeDelta = new Vector2(36f, 36f);

        Image iconImage = icon.GetComponent<Image>();
        iconImage.color = iconPlaceholderColor;
        iconImage.raycastTarget = false;

        GameObject nameObject = new GameObject("Name", typeof(RectTransform), typeof(Text));
        nameObject.transform.SetParent(item.transform, false);

        RectTransform nameRect = nameObject.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = new Vector2(54f, 27f);
        nameRect.offsetMax = new Vector2(-12f, -4f);

        Text nameText = nameObject.GetComponent<Text>();
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.color = new Color(1f, 1f, 1f, 0.75f);
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = 15;
        nameText.fontStyle = FontStyle.Bold;
        nameText.raycastTarget = false;
        nameText.text = resourceName;
        AddTextShadow(nameObject);

        GameObject label = new GameObject("Value", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(item.transform, false);

        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(54f, 0f);
        labelRect.offsetMax = new Vector2(-12f, -2f);

        Text text = label.GetComponent<Text>();
        text.alignment = TextAnchor.MiddleRight;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 30;
        text.fontStyle = FontStyle.Bold;
        text.raycastTarget = false;
        text.text = "0";
        AddTextShadow(label);

        return text;
    }

    private void ApplyReadableDefaults()
    {
        itemSize.x = Mathf.Max(itemSize.x, 150f);
        itemSize.y = Mathf.Max(itemSize.y, 54f);
        itemSpacing = Mathf.Max(itemSpacing, 10f);

        if (backgroundColor.a < 0.6f)
        {
            backgroundColor = new Color(0f, 0f, 0f, 0.68f);
        }

        if (iconPlaceholderColor.a < 0.3f)
        {
            iconPlaceholderColor = new Color(1f, 1f, 1f, 0.35f);
        }
    }

    private void AddTextShadow(GameObject textObject)
    {
        Shadow shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        shadow.useGraphicAlpha = true;
    }

    private void SetText(Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private void ForceNextRefresh()
    {
        lastCoins = int.MinValue;
        lastWood = int.MinValue;
        lastStone = int.MinValue;
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }
}
