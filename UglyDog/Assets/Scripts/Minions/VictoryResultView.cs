using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class VictoryResultView : MonoBehaviour
{
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
