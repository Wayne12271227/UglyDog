using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class VictoryResultView : MonoBehaviour
{
    private const string ConfirmButtonLabel = "\u78ba\u8a8d";

    [SerializeField] private Text resultText;
    [SerializeField] private RawImage characterImage;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    public Text ResultText
    {
        get
        {
            BindIfNeeded();
            return resultText;
        }
    }

    public RawImage CharacterImage
    {
        get
        {
            BindIfNeeded();
            return characterImage;
        }
    }

    private void Awake()
    {
        BindIfNeeded();
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }
    }

    public void ShowResult(string result, Texture characterTexture)
    {
        BindIfNeeded();

        if (resultText != null)
        {
            resultText.text = result;
        }

        SetCharacterTexture(characterTexture);
        gameObject.SetActive(true);
    }

    public void SetCharacterTexture(Texture characterTexture)
    {
        BindIfNeeded();

        if (characterImage == null)
        {
            return;
        }

        characterImage.texture = characterTexture;
        characterImage.enabled = characterTexture != null;
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void BindIfNeeded()
    {
        if (resultText == null)
        {
            resultText = FindChildComponent<Text>("Result Text");
        }

        if (characterImage == null)
        {
            characterImage = FindChildComponent<RawImage>("Victory Character Image");
        }

        if (mainMenuButton == null)
        {
            mainMenuButton = FindChildComponent<Button>("Return Main Menu Button");
        }

        if (mainMenuButton != null)
        {
            EnsureMainMenuButtonLabel();
        }
    }

    private void EnsureMainMenuButtonLabel()
    {
        Text buttonText = mainMenuButton.GetComponentInChildren<Text>(true);
        if (buttonText == null)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(mainMenuButton.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            buttonText = textObject.GetComponent<Text>();
        }

        Font readableFont = LoadReadableFont();
        if (readableFont != null)
        {
            buttonText.font = readableFont;
        }

        buttonText.text = ConfirmButtonLabel;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.fontSize = 34;
        buttonText.fontStyle = FontStyle.Bold;
        buttonText.color = new Color(0.08f, 0.08f, 0.1f, 1f);
        buttonText.raycastTarget = false;
    }

    private static Font LoadReadableFont()
    {
        Font font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft JhengHei", "Microsoft YaHei", "Arial Unicode MS", "Noto Sans CJK TC" },
            18);

        if (font != null)
        {
            return font;
        }

        try
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch (System.ArgumentException)
        {
            return null;
        }
    }

    private T FindChildComponent<T>(string childName) where T : Component
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
            {
                return children[i].GetComponent<T>();
            }
        }

        return null;
    }
}
