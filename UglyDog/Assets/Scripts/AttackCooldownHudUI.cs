using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class AttackCooldownHudUI : MonoBehaviour
{
    [SerializeField] private Sprite attackIcon;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.58f);
    [SerializeField] private Color readyColor = Color.white;
    [SerializeField] private Color cooldownGray = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color cooldownTextColor = Color.white;
    [SerializeField] private string grayscaleShaderName = "UglyDog/UI/Grayscale";
    [SerializeField] private float playerRefreshInterval = 0.35f;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image grayIconImage;
    [SerializeField] private Image readyIconImage;
    [SerializeField] private Text cooldownText;

    private Material grayIconMaterial;
    private CatPlayerController player;
    private float nextPlayerRefreshTime;
    private bool missingReferencesWarned;

    private void Awake()
    {
        CachePrefabReferences();
        ConfigurePrefabReferences();
    }

    private void OnEnable()
    {
        RefreshPlayer(true);
        UpdateCooldownVisuals();
    }

    private void Update()
    {
        RefreshPlayer(false);
        UpdateCooldownVisuals();
    }

    private void OnDestroy()
    {
        if (grayIconMaterial != null)
        {
            Destroy(grayIconMaterial);
        }
    }

    private void RefreshPlayer(bool force)
    {
        if (!force && player != null && Time.unscaledTime < nextPlayerRefreshTime)
        {
            return;
        }

        player = PreferredPlayerFinder.FindPreferredPlayer();
        nextPlayerRefreshTime = Time.unscaledTime + playerRefreshInterval;
    }

    private void UpdateCooldownVisuals()
    {
        if (!CachePrefabReferences())
        {
            WarnAboutMissingReferences();
            return;
        }

        ConfigurePrefabReferences();

        float readyFraction = player != null ? player.AttackCooldownReadyFraction : 1f;
        readyFraction = Mathf.Clamp01(readyFraction);

        readyIconImage.fillAmount = readyFraction;
        readyIconImage.color = readyColor;
        ApplyGrayMaterial();

        bool isCoolingDown = readyFraction < 0.999f;
        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(isCoolingDown);
            if (isCoolingDown)
            {
                cooldownText.text = Mathf.CeilToInt(player.AttackCooldownRemaining).ToString();
            }
        }
    }

    private bool CachePrefabReferences()
    {
        if (backgroundImage == null)
        {
            backgroundImage = FindChildImage("Attack Cooldown Background");
        }

        if (grayIconImage == null)
        {
            grayIconImage = FindChildImage("Attack Icon Gray");
        }

        if (readyIconImage == null)
        {
            readyIconImage = FindChildImage("Attack Icon Ready Fill");
        }

        if (cooldownText == null)
        {
            cooldownText = FindChildText("Attack Cooldown Text");
        }

        return grayIconImage != null && readyIconImage != null;
    }

    private void ConfigurePrefabReferences()
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
            backgroundImage.raycastTarget = false;
        }

        if (grayIconImage != null)
        {
            SetupIconImage(grayIconImage);
            grayIconImage.type = Image.Type.Simple;
            ApplyGrayMaterial();
        }

        if (readyIconImage != null)
        {
            SetupIconImage(readyIconImage);
            readyIconImage.type = Image.Type.Filled;
            readyIconImage.fillMethod = Image.FillMethod.Radial360;
            readyIconImage.fillOrigin = (int)Image.Origin360.Top;
            readyIconImage.fillClockwise = true;
        }

        if (cooldownText != null)
        {
            cooldownText.alignment = TextAnchor.MiddleCenter;
            cooldownText.fontSize = 30;
            cooldownText.fontStyle = FontStyle.Bold;
            cooldownText.color = cooldownTextColor;
            cooldownText.raycastTarget = false;
        }
    }

    private void SetupIconImage(Image image)
    {
        if (attackIcon != null)
        {
            image.sprite = attackIcon;
        }

        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void ApplyGrayMaterial()
    {
        if (grayIconImage == null)
        {
            return;
        }

        Shader grayscaleShader = Shader.Find(grayscaleShaderName);
        if (grayscaleShader == null)
        {
            grayIconImage.material = null;
            grayIconImage.color = cooldownGray;
            return;
        }

        if (grayIconMaterial == null || grayIconMaterial.shader != grayscaleShader)
        {
            if (grayIconMaterial != null)
            {
                Destroy(grayIconMaterial);
            }

            grayIconMaterial = new Material(grayscaleShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        grayIconMaterial.SetColor("_GrayColor", cooldownGray);
        grayIconImage.material = grayIconMaterial;
        grayIconImage.color = Color.white;
    }

    private Image FindChildImage(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private Text FindChildText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Text>() : null;
    }

    private void WarnAboutMissingReferences()
    {
        if (missingReferencesWarned)
        {
            return;
        }

        missingReferencesWarned = true;
        Debug.LogWarning(
            $"{nameof(AttackCooldownHudUI)} needs prefab children named 'Attack Icon Gray' and 'Attack Icon Ready Fill'. It will not create UI objects at runtime.",
            this);
    }
}
