using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class GameplayPingDisplay : MonoBehaviour
{
    [SerializeField] private Button settingsButton;
    [SerializeField] private Text pingText;
    [SerializeField] private Vector2 labelSize = new Vector2(200f, 30f);
    [SerializeField] private Vector2 labelOffset = new Vector2(0f, -76f);
    [SerializeField] private float refreshInterval = 0.5f;

    private float nextRefreshTime;

    private void Awake()
    {
        EnsureText();
        RefreshPingText();
    }

    private void OnEnable()
    {
        EnsureText();
        RefreshPingText();
    }

    private void Update()
    {
        EnsureText();

        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        RefreshPingText();
    }

    public void Bind(Button button)
    {
        settingsButton = button;
        EnsureText();
        RefreshPingText();
    }

    private void EnsureText()
    {
        if (settingsButton == null)
        {
            GameObject buttonObject = GameObject.Find("Gameplay Settings Button");
            if (buttonObject != null)
            {
                settingsButton = buttonObject.GetComponent<Button>();
            }
        }

        if (settingsButton == null)
        {
            return;
        }

        if (pingText == null)
        {
            pingText = FindExistingText();
        }

        if (pingText == null)
        {
            CreateText();
        }

        PositionText();
    }

    private Text FindExistingText()
    {
        Transform parent = settingsButton.transform.parent;
        if (parent == null)
        {
            return null;
        }

        Transform existing = parent.Find("Ping Value Text");
        return existing != null ? existing.GetComponent<Text>() : null;
    }

    private void CreateText()
    {
        Transform parent = settingsButton.transform.parent;
        if (parent == null)
        {
            return;
        }

        GameObject textObject = new GameObject("Ping Value Text", typeof(RectTransform), typeof(CanvasRenderer));
        textObject.transform.SetParent(parent, false);

        pingText = textObject.AddComponent<Text>();
        pingText.font = UglyDogUIFont.Load();
        pingText.fontSize = 22;
        pingText.fontStyle = FontStyle.Bold;
        pingText.alignment = TextAnchor.MiddleCenter;
        pingText.color = Color.white;
        pingText.raycastTarget = false;
    }

    private void PositionText()
    {
        RectTransform buttonRect = settingsButton.transform as RectTransform;
        RectTransform textRect = pingText != null ? pingText.transform as RectTransform : null;
        if (buttonRect == null || textRect == null)
        {
            return;
        }

        textRect.anchorMin = buttonRect.anchorMin;
        textRect.anchorMax = buttonRect.anchorMax;
        textRect.pivot = buttonRect.pivot;
        textRect.sizeDelta = labelSize;
        textRect.anchoredPosition = buttonRect.anchoredPosition + labelOffset;
        textRect.localScale = Vector3.one;
    }

    private void RefreshPingText()
    {
        if (pingText == null)
        {
            return;
        }

        if (TryGetPingMilliseconds(out int pingMs))
        {
            pingText.text = "Ping: " + pingMs + " ms";
            return;
        }

        pingText.text = "Ping: -- ms";
    }

    private bool TryGetPingMilliseconds(out int pingMs)
    {
        pingMs = 0;
        NetworkRunner[] runners = FindObjectsOfType<NetworkRunner>();
        for (int i = 0; i < runners.Length; i++)
        {
            NetworkRunner runner = runners[i];
            if (runner == null || !runner.IsRunning)
            {
                continue;
            }

            double rttSeconds = runner.GetPlayerRtt(runner.LocalPlayer);
            if (double.IsNaN(rttSeconds) || rttSeconds < 0.0)
            {
                continue;
            }

            pingMs = Mathf.Max(0, Mathf.RoundToInt((float)(rttSeconds * 1000.0)));
            return true;
        }

        return false;
    }
}
