using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "RoomLobby";
    [SerializeField] private string startButtonName = "Start Button";
    [SerializeField] private string settingsButtonName = "setting Button ";
    [SerializeField] private string exitButtonName = "Exit Button";

    private void Awake()
    {
        EnsureEventSystem();
        EnsureCanvasCanReceiveClicks();
        DisableDecorativeRaycasts();
        WireMenuButtons();
        EnsureButtonHoverTints();
    }

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenSettings()
    {
        Debug.Log("Settings menu is not implemented yet.");
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void WireMenuButtons()
    {
        BindButton(FindButton(startButtonName), StartGame);
        BindButton(FindButton(settingsButtonName), OpenSettings);
        BindButton(FindButton(exitButtonName), ExitGame);
    }

    private Button FindButton(string buttonName)
    {
        if (string.IsNullOrWhiteSpace(buttonName))
        {
            return null;
        }

        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == buttonName)
            {
                return button;
            }
        }

        return null;
    }

    private static void BindButton(Button button, UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.enabled = true;
        button.interactable = true;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
            button.targetGraphic = image;
        }
    }

    private void EnsureButtonHoverTints()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.GetComponent<ButtonHoverTint>() == null)
            {
                button.gameObject.AddComponent<ButtonHoverTint>();
            }
        }
    }

    private void EnsureCanvasCanReceiveClicks()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = true;
        }

        GraphicRaycaster raycaster = GetComponentInParent<GraphicRaycaster>();
        if (raycaster == null && canvas != null)
        {
            raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        if (raycaster != null)
        {
            raycaster.enabled = true;
        }
    }

    private void DisableDecorativeRaycasts()
    {
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.GetComponentInParent<Button>() == null)
            {
                graphic.raycastTarget = false;
            }
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }
}
