using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameplaySettingsController : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;
    [SerializeField] private Button settingsButton;
    [SerializeField] private SettingsPanelUI settingsPanel;

    private void Awake()
    {
        EnsureEventSystem();
        BindSceneReferences();

        if (settingsPanel != null)
        {
            settingsPanel.SetExitGameButtonVisible(true);
            settingsPanel.Hide();
        }
    }

    private void OnEnable()
    {
        BindSceneReferences();
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OpenSettings);
            settingsButton.onClick.AddListener(OpenSettings);
        }
    }

    private void OnDisable()
    {
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OpenSettings);
        }
    }

    private void Update()
    {
        if (settingsPanel != null && settingsPanel.gameObject.activeSelf)
        {
            return;
        }

        if (Input.GetKeyDown(toggleKey)
            && !BuildingPlacementController.BlocksPlayerInput
            && !UpgradeShopUI.BlocksPlayerInput
            && !BuildShopUI.BlocksPlayerInput)
        {
            OpenSettings();
        }
    }

    private void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.Show();
        }
    }

    private void BindSceneReferences()
    {
        if (settingsPanel == null)
        {
            settingsPanel = FindObjectOfType<SettingsPanelUI>(true);
        }

        if (settingsButton == null)
        {
            GameObject buttonObject = GameObject.Find("Gameplay Settings Button");
            if (buttonObject != null)
            {
                settingsButton = buttonObject.GetComponent<Button>();
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
