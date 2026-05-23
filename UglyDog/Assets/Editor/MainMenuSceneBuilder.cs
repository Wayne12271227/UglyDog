using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MainMenuSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string GameScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MenuImagePath = "Assets/image2D/menuImage.png";
    private const string CatPrefabPath = "Assets/prefab/character/CAT2 1.prefab";
    private const string DogPrefabPath = "Assets/prefab/character/DOG.prefab";
    private const string MaterialsFolder = "Assets/ToonURP/Materials";
    private const string BackgroundMaterialPath = MaterialsFolder + "/MenuBackgroundUnlit.mat";
    private const string ContactShadowMaterialPath = MaterialsFolder + "/MenuContactShadow.mat";
    private const string OutlineMaterialPath = MaterialsFolder + "/DefaultToonOutline.mat";
    private const string DogToonMaterialPath = MaterialsFolder + "/DogToon.mat";
    private const string DogOutlineMaterialPath = MaterialsFolder + "/DogOutline.mat";
    private const string OutlineShaderName = "Custom/URPToonOutline";
    private const string ToonShaderName = "Custom/ToonLitOutline";

    private const float CameraZ = -10f;
    private const float BackgroundZ = 12f;
    private const float CharacterZ = 2.4f;
    private const float CharacterGroundY = -3.35f;

    static MainMenuSceneBuilder()
    {
        EditorApplication.delayCall += BuildSceneIfNeeded;
    }

    [MenuItem("Tools/Build Main Menu Scene")]
    public static void BuildScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += BuildScene;
            return;
        }

        Texture2D backgroundTexture = LoadMenuTexture();
        if (backgroundTexture == null)
        {
            EditorUtility.DisplayDialog("Build Main Menu Scene", "Could not load " + MenuImagePath + ".", "OK");
            return;
        }

        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        Camera camera = CreateCamera();
        CreateLighting();
        CreateBackgroundPlane(backgroundTexture);
        CreateCharacterStage();

        Canvas canvas = CreateCanvas(camera);
        MainMenuController controller = canvas.gameObject.AddComponent<MainMenuController>();
        CreateButtonPanel(canvas.transform);
        CreateButton(canvas.transform, "Start Button", "\u958b\u59cb\u904a\u6232", new Vector2(0f, -248f), controller.StartGame);
        CreateButton(canvas.transform, "Exit Button", "\u96e2\u958b\u904a\u6232", new Vector2(0f, -328f), controller.ExitGame);
        CreateEventSystem();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath, true, 0);
        AddSceneToBuildSettings(GameScenePath, true, 1);
        AssetDatabase.SaveAssets();

        if (previousScene.IsValid() && !string.IsNullOrEmpty(previousScene.path) && previousScene.path != ScenePath)
        {
            EditorSceneManager.OpenScene(previousScene.path, OpenSceneMode.Single);
        }
    }

    private static void BuildSceneIfNeeded()
    {
        if (!SceneNeedsBuild())
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += BuildSceneIfNeeded;
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BuildSceneIfNeeded;
            return;
        }

        BuildScene();
    }

    private static bool SceneNeedsBuild()
    {
        if (!File.Exists(ScenePath))
        {
            return true;
        }

        string sceneText = File.ReadAllText(ScenePath);
        return !sceneText.Contains("Menu 3D Stage")
            || !sceneText.Contains("Menu Background Plane")
            || sceneText.Contains("Menu Foreground Ground Mask")
            || !sceneText.Contains("Menu Contact Shadows");
    }

    private static Texture2D LoadMenuTexture()
    {
        TextureImporter importer = AssetImporter.GetAtPath(MenuImagePath) as TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(MenuImagePath);
    }

    private static Camera CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.transform.position = new Vector3(0f, 0f, CameraZ);
        cameraObject.transform.rotation = Quaternion.identity;
        cameraObject.tag = "MainCamera";

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.52f, 0.68f, 0.76f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5.4f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 50f;
        return camera;
    }

    private static void CreateLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.68f, 0.72f, 0.68f, 1f);
        RenderSettings.reflectionIntensity = 0.25f;

        GameObject keyLightObject = new GameObject("Menu Key Light");
        keyLightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
        Light keyLight = keyLightObject.AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.color = new Color(1f, 0.93f, 0.82f, 1f);
        keyLight.intensity = 1.35f;
        keyLight.shadows = LightShadows.Soft;

        GameObject fillLightObject = new GameObject("Menu Fill Light");
        fillLightObject.transform.rotation = Quaternion.Euler(-18f, 130f, 0f);
        Light fillLight = fillLightObject.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.color = new Color(0.58f, 0.72f, 1f, 1f);
        fillLight.intensity = 0.35f;
        fillLight.shadows = LightShadows.None;
    }

    private static void CreateBackgroundPlane(Texture2D texture)
    {
        Material material = EnsureBackgroundMaterial(texture);

        GameObject backgroundObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backgroundObject.name = "Menu Background Plane";
        backgroundObject.transform.position = new Vector3(0f, 0f, BackgroundZ);
        backgroundObject.transform.rotation = Quaternion.identity;
        backgroundObject.transform.localScale = new Vector3(19.2f, 10.8f, 1f);

        MeshRenderer renderer = backgroundObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Object.DestroyImmediate(backgroundObject.GetComponent<Collider>());
    }

    private static void CreateCharacterStage()
    {
        GameObject stage = new GameObject("Menu 3D Stage");
        Transform shadowRoot = new GameObject("Menu Contact Shadows").transform;
        shadowRoot.SetParent(stage.transform, false);

        Material outlineMaterial = EnsureOutlineMaterial();

        GameObject catPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CatPrefabPath);
        if (catPrefab != null)
        {
            GameObject cat = PrefabUtility.InstantiatePrefab(catPrefab) as GameObject;
            if (cat != null)
            {
                cat.name = "Menu CAT";
                cat.transform.SetParent(stage.transform, true);
                FitCharacterToMenu(cat, -1.65f, CharacterGroundY, CharacterZ, 2.55f);
                ApplyMenuToon(cat, outlineMaterial);
                PrepareMenuCharacter(cat);
                CreateContactShadow(shadowRoot, "CAT Contact Shadow", cat, 0.9f, 0.3f);
            }
        }

        GameObject dogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DogPrefabPath);
        if (dogPrefab != null)
        {
            GameObject dog = PrefabUtility.InstantiatePrefab(dogPrefab) as GameObject;
            if (dog != null)
            {
                dog.name = "Menu DOG";
                dog.transform.SetParent(stage.transform, true);
                FitCharacterToMenu(dog, 1.55f, CharacterGroundY, CharacterZ + 0.1f, 2.25f);
                Material dogToonMaterial = AssetDatabase.LoadAssetAtPath<Material>(DogToonMaterialPath);
                Material dogOutlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(DogOutlineMaterialPath);
                ApplyMenuToon(dog, dogOutlineMaterial != null ? dogOutlineMaterial : outlineMaterial, dogToonMaterial, false);
                PrepareMenuCharacter(dog);
                CreateContactShadow(shadowRoot, "DOG Contact Shadow", dog, 0.85f, 0.28f);
            }
        }
    }

    private static void FitCharacterToMenu(GameObject character, float targetCenterX, float targetGroundY, float targetZ, float targetHeight)
    {
        Bounds bounds = CalculateRendererBounds(character);
        if (bounds.size.y > 0.01f)
        {
            float scaleMultiplier = targetHeight / bounds.size.y;
            character.transform.localScale *= scaleMultiplier;
        }

        bounds = CalculateRendererBounds(character);
        Vector3 offset = new Vector3(
            targetCenterX - bounds.center.x,
            targetGroundY - bounds.min.y,
            targetZ - bounds.center.z);
        character.transform.position += offset;
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer.GetComponent<CharacterOutlineProxy>() == null)
            .ToArray();

        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static Canvas CreateCanvas(Camera camera)
    {
        GameObject canvasObject = new GameObject("Main Menu Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void CreateButtonPanel(Transform parent)
    {
        GameObject panelObject = new GameObject("Button Backdrop");
        panelObject.transform.SetParent(parent, false);

        Image panel = panelObject.AddComponent<Image>();
        panel.color = new Color(0.08f, 0.06f, 0.04f, 0.42f);

        RectTransform rectTransform = panel.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(0f, -288f);
        rectTransform.sizeDelta = new Vector2(340f, 176f);
    }

    private static void CreateButton(Transform parent, string objectName, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(objectName);
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.96f, 0.82f, 0.46f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.96f, 0.82f, 0.46f, 0.95f);
        colors.highlightedColor = new Color(1f, 0.91f, 0.58f, 1f);
        colors.pressedColor = new Color(0.78f, 0.58f, 0.28f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        UnityEventTools.AddPersistentListener(button.onClick, action);

        RectTransform rectTransform = button.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = new Vector2(280f, 58f);

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(buttonObject.transform, false);

        Text text = textObject.AddComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 30;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.16f, 0.11f, 0.07f, 1f);

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private static void ApplyMenuToon(GameObject characterRoot, Material outlineMaterial, Material baseToonMaterial = null, bool preserveExistingMaterialTextures = true)
    {
        ToonCharacterSetup setup = characterRoot.GetComponent<ToonCharacterSetup>();
        if (setup == null)
        {
            setup = characterRoot.AddComponent<ToonCharacterSetup>();
        }

        SerializedObject serializedSetup = new SerializedObject(setup);
        serializedSetup.FindProperty("targetRootName").stringValue = characterRoot.name;
        serializedSetup.FindProperty("targetRoot").objectReferenceValue = characterRoot.transform;
        serializedSetup.FindProperty("baseToonMaterial").objectReferenceValue = baseToonMaterial;
        serializedSetup.FindProperty("toonShaderName").stringValue = ToonShaderName;
        serializedSetup.FindProperty("outlineMaterial").objectReferenceValue = outlineMaterial;
        serializedSetup.FindProperty("enableOutline").boolValue = true;
        serializedSetup.FindProperty("preserveExistingMaterialTextures").boolValue = preserveExistingMaterialTextures;
        serializedSetup.FindProperty("baseColor").colorValue = Color.white;
        serializedSetup.FindProperty("shadowColor").colorValue = new Color(0.68f, 0.55f, 0.44f, 1f);
        serializedSetup.FindProperty("shadowThreshold").floatValue = 0.38f;
        serializedSetup.FindProperty("shadowSmoothness").floatValue = 0.05f;
        serializedSetup.FindProperty("rimColor").colorValue = new Color(1f, 0.9f, 0.72f, 1f);
        serializedSetup.FindProperty("rimPower").floatValue = 3.2f;
        serializedSetup.FindProperty("rimStrength").floatValue = 0.24f;
        serializedSetup.FindProperty("outlineColor").colorValue = new Color(0.12f, 0.07f, 0.05f, 1f);
        serializedSetup.FindProperty("outlineWidth").floatValue = 0.011f;
        serializedSetup.ApplyModifiedPropertiesWithoutUndo();

        setup.ApplyToonStyle();

        foreach (Renderer renderer in characterRoot.GetComponentsInChildren<Renderer>(true))
        {
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.receiveShadows = false;
        }
    }

    private static void PrepareMenuCharacter(GameObject characterRoot)
    {
        foreach (CatPlayerController controller in characterRoot.GetComponentsInChildren<CatPlayerController>(true))
        {
            controller.enabled = false;
        }

        foreach (Rigidbody body in characterRoot.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        foreach (Collider collider in characterRoot.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (Animator animator in characterRoot.GetComponentsInChildren<Animator>(true))
        {
            animator.applyRootMotion = false;
            SetAnimatorFloatIfExists(animator, "Speed", 0f);
            ResetAnimatorTriggerIfExists(animator, "Attack");
            ResetAnimatorTriggerIfExists(animator, "Dig");
            ResetAnimatorTriggerIfExists(animator, "Build");
        }

        if (characterRoot.GetComponent<MenuCharacterIdleLock>() == null)
        {
            characterRoot.AddComponent<MenuCharacterIdleLock>();
        }
    }

    private static void CreateContactShadow(Transform parent, string objectName, GameObject characterRoot, float width, float height)
    {
        Bounds bounds = CalculateRendererBounds(characterRoot);

        GameObject shadowObject = new GameObject(objectName);
        shadowObject.transform.SetParent(parent, true);
        shadowObject.transform.position = new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.max.z + 0.05f);
        shadowObject.transform.rotation = Quaternion.identity;

        MeshFilter meshFilter = shadowObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CreateEllipseMesh(width, height, 48);

        MeshRenderer renderer = shadowObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = EnsureContactShadowMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private static Mesh CreateEllipseMesh(float width, float height, int segments)
    {
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;

        float radiusX = width * 0.5f;
        float radiusY = height * 0.5f;
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY, 0f);
        }

        for (int i = 0; i < segments; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i == segments - 1 ? 1 : i + 2;
        }

        Mesh mesh = new Mesh
        {
            name = "MenuContactShadowMesh",
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void SetAnimatorFloatIfExists(Animator animator, string parameterName, float value)
    {
        if (animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Float)
            {
                animator.SetFloat(parameterName, value);
                return;
            }
        }
    }

    private static void ResetAnimatorTriggerIfExists(Animator animator, string parameterName)
    {
        if (animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.ResetTrigger(parameterName);
                return;
            }
        }
    }

    private static Material EnsureBackgroundMaterial(Texture2D texture)
    {
        EnsureFolder(MaterialsFolder);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(BackgroundMaterialPath);
        if (material == null)
        {
            material = new Material(ResolveUnlitShader());
            AssetDatabase.CreateAsset(material, BackgroundMaterialPath);
        }

        material.shader = ResolveUnlitShader();
        SetTextureIfAvailable(material, "_BaseMap", texture);
        SetTextureIfAvailable(material, "_MainTex", texture);
        SetColorIfAvailable(material, "_BaseColor", Color.white);
        SetColorIfAvailable(material, "_Color", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material EnsureContactShadowMaterial()
    {
        EnsureFolder(MaterialsFolder);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(ContactShadowMaterialPath);
        if (material == null)
        {
            material = new Material(ResolveTransparentUnlitShader());
            AssetDatabase.CreateAsset(material, ContactShadowMaterialPath);
        }

        material.shader = ResolveTransparentUnlitShader();
        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
        SetColorIfAvailable(material, "_BaseColor", new Color(0.08f, 0.06f, 0.04f, 0.24f));
        SetColorIfAvailable(material, "_Color", new Color(0.08f, 0.06f, 0.04f, 0.24f));
        SetFloatIfAvailable(material, "_Surface", 1f);
        SetFloatIfAvailable(material, "_Blend", 0f);
        SetFloatIfAvailable(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloatIfAvailable(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfAvailable(material, "_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material EnsureOutlineMaterial()
    {
        EnsureFolder(MaterialsFolder);

        Shader outlineShader = Shader.Find(OutlineShaderName);
        if (outlineShader == null)
        {
            outlineShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/URPToonOutline.shader");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
        if (material == null && outlineShader != null)
        {
            material = new Material(outlineShader);
            AssetDatabase.CreateAsset(material, OutlineMaterialPath);
        }

        if (material != null && outlineShader != null)
        {
            material.shader = outlineShader;
            SetColorIfAvailable(material, "_OutlineColor", new Color(0.12f, 0.07f, 0.05f, 1f));
            SetFloatIfAvailable(material, "_OutlineWidth", 0.011f);
            EditorUtility.SetDirty(material);
        }

        return material;
    }

    private static Shader ResolveUnlitShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Texture");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return shader;
    }

    private static Shader ResolveTransparentUnlitShader()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = ResolveUnlitShader();
        }

        return shader;
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private static void AddSceneToBuildSettings(string scenePath, bool enabled, int desiredIndex)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
            .Where(scene => scene.path != scenePath)
            .ToList();

        EditorBuildSettingsScene entry = new EditorBuildSettingsScene(scenePath, enabled);
        int index = Mathf.Clamp(desiredIndex, 0, scenes.Count);
        scenes.Insert(index, entry);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static void SetTextureIfAvailable(Material material, string propertyName, Texture texture)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, texture);
        }
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
}
