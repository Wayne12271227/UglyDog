using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;

public class VictoryResultView : MonoBehaviour
{
    private const string ConfirmButtonLabel = "\u78ba\u8a8d";

    [SerializeField] private Text resultText;
    [SerializeField] private RawImage characterImage;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private Text confirmButtonText;
    private UnityAction confirmAction;
    private string confirmButtonLabel = ConfirmButtonLabel;

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
        SetConfirmAction(confirmAction ?? ReturnToMainMenu);
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

    public void SetConfirmAction(UnityAction action)
    {
        BindIfNeeded();

        confirmAction = action ?? ReturnToMainMenu;
        if (mainMenuButton == null)
        {
            return;
        }

        mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
        mainMenuButton.onClick.RemoveListener(InvokeConfirmAction);
        mainMenuButton.onClick.AddListener(InvokeConfirmAction);
    }

    public void SetConfirmButtonLabel(string label)
    {
        confirmButtonLabel = string.IsNullOrWhiteSpace(label) ? ConfirmButtonLabel : label;
        EnsureMainMenuButtonLabel();
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

    private void InvokeConfirmAction()
    {
        if (confirmAction != null)
        {
            confirmAction.Invoke();
            return;
        }

        ReturnToMainMenu();
    }

    private void EnsureMainMenuButtonLabel()
    {
        if (mainMenuButton == null)
        {
            return;
        }

        confirmButtonText = mainMenuButton.GetComponentInChildren<Text>(true);
        bool createdLabel = confirmButtonText == null;
        if (confirmButtonText == null)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(mainMenuButton.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            confirmButtonText = textObject.GetComponent<Text>();
        }

        if (createdLabel || confirmButtonText.font == null)
        {
            Font readableFont = LoadReadableFont();
            if (readableFont != null)
            {
                confirmButtonText.font = readableFont;
            }
        }

        confirmButtonText.text = confirmButtonLabel;
        confirmButtonText.alignment = TextAnchor.MiddleCenter;
        if (createdLabel)
        {
            confirmButtonText.fontSize = 34;
            confirmButtonText.fontStyle = FontStyle.Bold;
            confirmButtonText.color = Color.white;
        }

        confirmButtonText.raycastTarget = false;
    }

    private static Font LoadReadableFont()
    {
        return UglyDogUIFont.Load();
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
