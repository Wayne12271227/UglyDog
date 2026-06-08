using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameplaySettingsController : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;
    [SerializeField] private string settingsPanelName = "Gameplay Settings Panel";
    [SerializeField] private Button settingsButton;
    [SerializeField] private SettingsPanelUI settingsPanel;
    [SerializeField] private GameplayPingDisplay pingDisplay;

    private void Awake()
    {
        EnsureEventSystem();
        BindSceneReferences();

        if (settingsPanel != null)
        {
            settingsPanel.SetExitGameButtonVisible(true);
            settingsPanel.Hide();
        }

        EnsurePingDisplay();
    }

    private void OnEnable()
    {
        BindSceneReferences();
        PrepareSettingsPanel();
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OpenSettings);
            settingsButton.onClick.AddListener(OpenSettings);
        }

        EnsurePingDisplay();
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
        BindSceneReferences();

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
        BindSceneReferences();
        PrepareSettingsPanel();
        if (settingsPanel != null)
        {
            settingsPanel.Show();
        }
    }

    private void BindSceneReferences()
    {
        if (settingsPanel == null)
        {
            settingsPanel = FindGameplaySettingsPanel();
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

    private void PrepareSettingsPanel()
    {
        if (settingsPanel == null)
        {
            return;
        }

        settingsPanel.SetExitGameButtonVisible(true);
    }

    private SettingsPanelUI FindGameplaySettingsPanel()
    {
        SettingsPanelUI[] panels = Resources.FindObjectsOfTypeAll<SettingsPanelUI>();
        SettingsPanelUI fallback = null;
        for (int i = 0; i < panels.Length; i++)
        {
            SettingsPanelUI panel = panels[i];
            if (panel == null || !panel.gameObject.scene.IsValid())
            {
                continue;
            }

            if (!string.IsNullOrEmpty(settingsPanelName) && panel.name == settingsPanelName)
            {
                return panel;
            }

            if (fallback == null)
            {
                fallback = panel;
            }
        }

        return fallback;
    }

    private void EnsurePingDisplay()
    {
        if (pingDisplay == null)
        {
            pingDisplay = GetComponent<GameplayPingDisplay>();
        }

        if (pingDisplay == null)
        {
            pingDisplay = gameObject.AddComponent<GameplayPingDisplay>();
        }

        pingDisplay.Bind(settingsButton);
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
