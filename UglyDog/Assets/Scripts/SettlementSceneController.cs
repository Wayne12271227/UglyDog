using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettlementSceneController : MonoBehaviour
{
    private const string SettlementSceneName = "settlement";
    private const string MainMenuSceneName = "MainMenu";
    private const string ToonShaderName = "Custom/ToonLitOutline";

    private static readonly Vector3 WinnerViewportPosition = new Vector3(0.62f, 0.42f, 8f);
    private const float WinnerTargetHeight = 2.65f;

    private static readonly string[] DogWinnerObjectNames = { "Menu DOG", "DOG", "Dog", "dog" };
    private static readonly string[] CatWinnerObjectNames = { "CAT2 1", "CAT2", "CAT", "Menu CAT", "Cat", "cat" };

    private GameObject previewInstance;
    private bool previewInstanceIsRuntimeClone;
    private Material runtimeOutlineMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForCurrentScene()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreateForScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreateForScene(scene);
    }

    private static void TryCreateForScene(Scene scene)
    {
        if (!string.Equals(scene.name, SettlementSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (FindObjectOfType<SettlementSceneController>() != null)
        {
            return;
        }

        new GameObject("Settlement Scene Controller").AddComponent<SettlementSceneController>();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        EnsureEventSystem();
        BuildSettlementUi();
        BindExistingReturnButton();
        ShowWinnerModel();
        EnsureSettlementMusic();
    }

    private void OnDestroy()
    {
        DestroyWinnerModel();
    }

    private void BuildSettlementUi()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Settlement Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Transform root = canvas.transform;
        ClearGeneratedChildren(root);

        CreateLeftInfo(root);
        CreateWinnerLabel(root);
    }

    private void CreateLeftInfo(Transform root)
    {
        RectTransform group = CreateRect("Settlement Info", root, new Vector2(660f, 540f), new Vector2(120f, 18f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

        Text title = CreateText(group, "Title", "\u6230\u9b25\u7d50\u7b97", 64, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0f, 178f), new Vector2(660f, 96f));
        title.color = Color.white;

        string winner = GetTeamName(SettlementResultData.WinningTeam);
        string loser = GetTeamName(SettlementResultData.LosingTeam);
        string duration = FormatDuration(SettlementResultData.BattleDurationSeconds);
        string resultText = SettlementResultData.HasResult
            ? "\u52dd\u5229\u8005\uff1a" + winner + "\n\u5931\u6557\u65b9\uff1a" + loser + "\n\u672c\u5834\u6230\u9b25\u7528\u6642\uff1a" + duration
            : "\u5c1a\u672a\u53d6\u5f97\u6230\u9b25\u8cc7\u6599\n\u8acb\u5f9e\u904a\u6232\u52dd\u5229\u5f8c\u9032\u5165\u6b64\u9801";

        Text body = CreateText(group, "Result Details", resultText, 36, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0f, -12f), new Vector2(660f, 250f));
        body.lineSpacing = 1.18f;
        body.color = new Color(1f, 0.95f, 0.82f, 1f);

        Text hint = CreateText(group, "Hint", "\u6aa2\u8996\u7d50\u7b97\u5f8c\u53ef\u8fd4\u56de\u4e3b\u9078\u55ae", 24, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(0f, -214f), new Vector2(660f, 54f));
        hint.color = new Color(1f, 1f, 1f, 0.78f);
    }

    private void CreateWinnerLabel(Transform root)
    {
        string winner = GetTeamName(SettlementResultData.WinningTeam);
        RectTransform labelRect = CreateRect("Settlement Winner Label", root, new Vector2(620f, 96f), new Vector2(-145f, 325f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
        Text label = CreateText(labelRect, "Text", "\u52dd\u5229\u8005\uff1a" + winner, 46, FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, labelRect.sizeDelta);
        label.color = new Color(1f, 0.95f, 0.62f, 1f);
    }

    private void ShowWinnerModel()
    {
        DestroyWinnerModel();

        bool dogWon = SettlementResultData.WinningTeam == MinionTeam.Dog;
        GameObject dogObject = FindSceneObjectByNames(DogWinnerObjectNames);
        GameObject catObject = FindSceneObjectByNames(CatWinnerObjectNames);
        GameObject winnerObject = dogWon ? dogObject : catObject;
        GameObject loserObject = dogWon ? catObject : dogObject;

        Camera settlementCamera = ResolveSettlementCamera();
        if (settlementCamera == null)
        {
            return;
        }

        if (loserObject != null)
        {
            loserObject.SetActive(false);
        }

        if (winnerObject != null)
        {
            previewInstance = winnerObject;
            previewInstanceIsRuntimeClone = false;
            previewInstance.SetActive(true);
        }
        else
        {
            GameObject prefab = SettlementResultData.WinnerPrefab;
            if (prefab == null)
            {
                return;
            }

            previewInstance = Instantiate(prefab);
            previewInstanceIsRuntimeClone = true;
            previewInstance.name = GetTeamName(SettlementResultData.WinningTeam) + " Settlement Winner";
            previewInstance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            previewInstance.transform.localScale = Vector3.one;
            FitWinnerToSettlementCamera(previewInstance.transform, settlementCamera);
        }

        ApplyLayerRecursively(previewInstance, 0);
        PreparePreviewCharacter(previewInstance);
        if (previewInstanceIsRuntimeClone)
        {
            ApplyRuntimeToon(previewInstance);
        }
        StabilizeSettlementHighlights(previewInstance);
        EnsureModelLight();

        MenuCharacterIdleLock idleLock = previewInstance.GetComponent<MenuCharacterIdleLock>();
        if (idleLock == null)
        {
            idleLock = previewInstance.AddComponent<MenuCharacterIdleLock>();
        }

        idleLock.enabled = true;
    }

    private void DestroyWinnerModel()
    {
        if (previewInstance != null && previewInstanceIsRuntimeClone)
        {
            Destroy(previewInstance);
        }

        previewInstance = null;
        previewInstanceIsRuntimeClone = false;

        if (runtimeOutlineMaterial != null)
        {
            Destroy(runtimeOutlineMaterial);
            runtimeOutlineMaterial = null;
        }
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        PersistentBattleMusic.Stop();
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 anchoredPosition, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static Text CreateText(Transform parent, string name, string value, int size, FontStyle style, TextAnchor alignment, Vector2 position, Vector2 rectSize)
    {
        RectTransform rect = CreateRect(name, parent, rectSize, position, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = UglyDogUIFont.Load();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private static void ClearGeneratedChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child.name == "Settlement Info" || child.name == "Settlement Winner Label")
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void BindExistingReturnButton()
    {
        Button[] buttons = FindObjectsOfType<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !button.gameObject.scene.IsValid())
            {
                continue;
            }

            if (!string.Equals(button.gameObject.scene.name, SettlementSceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsReturnButton(button))
            {
                continue;
            }

            button.onClick.RemoveListener(ReturnToMainMenu);
            button.onClick.AddListener(ReturnToMainMenu);
            button.interactable = true;
            return;
        }
    }

    private static bool IsReturnButton(Button button)
    {
        string objectName = button.gameObject.name;
        if (!string.IsNullOrEmpty(objectName) && objectName.IndexOf("Back", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        Text[] texts = button.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].text.Contains("\u8fd4\u56de\u4e3b\u756b\u9762"))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureSettlementMusic()
    {
        PersistentBattleMusic.ResumeConfigured();
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static Camera ResolveSettlementCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            return mainCamera;
        }

        Camera[] cameras = FindObjectsOfType<Camera>();
        Camera bestCamera = null;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null || !candidate.isActiveAndEnabled)
            {
                continue;
            }

            if (bestCamera == null || candidate.depth > bestCamera.depth)
            {
                bestCamera = candidate;
            }
        }

        return bestCamera;
    }

    private static GameObject FindSceneObjectByNames(string[] names)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            string targetName = names[nameIndex];
            for (int objectIndex = 0; objectIndex < objects.Length; objectIndex++)
            {
                GameObject candidate = objects[objectIndex];
                if (candidate == null || !candidate.scene.IsValid())
                {
                    continue;
                }

                if (!string.Equals(candidate.scene.name, SettlementSceneName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(candidate.name, targetName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static void PreparePreviewCharacter(GameObject character)
    {
        Collider[] colliders = character.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] bodies = character.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].isKinematic = true;
            bodies[i].useGravity = false;
        }

        MonoBehaviour[] behaviours = character.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
            {
                behaviours[i].enabled = false;
            }
        }
    }

    private void ApplyRuntimeToon(GameObject character)
    {
        Shader toonShader = Shader.Find(ToonShaderName);
        if (toonShader == null)
        {
            return;
        }

        runtimeOutlineMaterial = new Material(toonShader)
        {
            name = "Settlement Runtime Toon Outline"
        };
        SetColorIfAvailable(runtimeOutlineMaterial, "_Color", Color.white);
        SetColorIfAvailable(runtimeOutlineMaterial, "_OutlineColor", new Color(0.14f, 0.08f, 0.06f, 1f));
        SetFloatIfAvailable(runtimeOutlineMaterial, "_OutlineWidth", 0.014f);

        ToonCharacterSetup setup = character.GetComponent<ToonCharacterSetup>();
        if (setup == null)
        {
            setup = character.AddComponent<ToonCharacterSetup>();
        }

        setup.Configure(character.transform, runtimeOutlineMaterial, null, true, true);
    }

    private static void StabilizeSettlementHighlights(GameObject character)
    {
        if (character == null)
        {
            return;
        }

        Renderer[] renderers = character.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer == null || renderer.GetComponent<CharacterOutlineProxy>() != null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                SetColorIfAvailable(material, "_RimColor", new Color(1f, 0.78f, 0.48f, 1f));
                SetFloatIfAvailable(material, "_RimStrength", 0.035f);
                SetFloatIfAvailable(material, "_ShadowThreshold", 0.22f);
                SetFloatIfAvailable(material, "_ShadowSmoothness", 0.055f);
            }
        }
    }

    private static void FitWinnerToSettlementCamera(Transform character, Camera camera)
    {
        if (character == null || camera == null || !TryGetRendererBounds(character, out Bounds bounds))
        {
            return;
        }

        float height = Mathf.Max(0.1f, bounds.size.y);
        character.localScale *= WinnerTargetHeight / height;

        if (!TryGetRendererBounds(character, out bounds))
        {
            return;
        }

        Vector3 targetCenter = camera.ViewportToWorldPoint(WinnerViewportPosition);
        character.position += targetCenter - bounds.center;
    }

    private static void ApplyLayerRecursively(GameObject target, int layer)
    {
        Transform[] transforms = target.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            transforms[i].gameObject.layer = layer;
        }
    }

    private static void EnsureModelLight()
    {
        Light[] lights = FindObjectsOfType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].isActiveAndEnabled)
            {
                return;
            }
        }

        GameObject lightObject = new GameObject("Settlement Winner Light", typeof(Light));
        lightObject.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(root.position, Vector3.one);
        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || !renderers[i].enabled)
            {
                continue;
            }

            if (renderers[i].GetComponent<CharacterOutlineProxy>() != null)
            {
                continue;
            }

            if (!found)
            {
                bounds = renderers[i].bounds;
                found = true;
                continue;
            }

            bounds.Encapsulate(renderers[i].bounds);
        }

        return found;
    }

    private static void SetColorIfAvailable(Material material, string propertyName, Color value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static void SetFloatIfAvailable(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static string FormatDuration(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return $"{minutes:00}:{remainingSeconds:00}";
    }

    private static string GetTeamName(MinionTeam team)
    {
        return team == MinionTeam.Dog ? "\u919c\u72d7" : "\u919c\u8c93";
    }
}
