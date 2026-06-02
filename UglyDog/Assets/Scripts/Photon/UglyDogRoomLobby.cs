using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UglyDogRoomLobby : MonoBehaviour, INetworkRunnerCallbacks
{
    private const int MaxPlayersPerRoom = 2;
    private const string MainMenuSceneName = "MainMenu";
    private const int LobbyFontSize = 30;
    private const float SpawnRangeMargin = 0.65f;
    private const float SpawnForwardWeight = 0.55f;

    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private NetworkObject secondPlayerPrefab;
    [SerializeField] private string gameSceneName = "SampleScene";
    [SerializeField] private string roomNamePrefix = "UglyDog";
    [SerializeField] private Vector3[] spawnPositions =
    {
        new Vector3(-2f, 0f, -2f),
        new Vector3(2f, 0f, -2f)
    };

    private readonly List<SessionInfo> sessions = new List<SessionInfo>();

    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;
    private NetworkObjectProviderDefault objectProvider;
    private Canvas canvas;
    private Camera lobbyCamera;
    private RectTransform roomListRoot;
    private Text titleText;
    private Text statusText;
    private Button refreshButton;
    private Button createRoomButton;
    private Button startGameButton;
    private Button leaveButton;
    private bool busy;
    private bool lobbyReady;
    private bool inRoom;
    private bool gameStarted;
    private bool gameplayScenePrepared;
    private bool isLeaving;

    private async void Start()
    {
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;

        BuildUi();
        busy = true;
        lobbyReady = false;
        SetStatus("正在連線到 Photon 大廳...");
        RefreshUi();

        runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        runner.AddCallbacks(this);
        sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
        objectProvider = gameObject.AddComponent<NetworkObjectProviderDefault>();

        StartGameResult result = await runner.JoinSessionLobby(SessionLobby.ClientServer);
        if (isLeaving || this == null)
        {
            return;
        }

        if (!result.Ok)
        {
            busy = false;
            lobbyReady = false;
            SetStatus($"Photon 大廳連線失敗：{result.ShutdownReason}");
            RefreshUi();
            return;
        }

        busy = false;
        lobbyReady = true;
        SetStatus("選擇房間或建立新房間。");
        RefreshUi();
    }

    private void Update()
    {
        if (isLeaving || !inRoom || gameplayScenePrepared)
        {
            return;
        }

        if (SceneManager.GetActiveScene().name == gameSceneName)
        {
            PrepareGameplayScene();
        }
    }

#if UNITY_EDITOR
    public void CreateEditorPreviewUi()
    {
        BuildUi();
        SetStatus("選擇房間或建立新房間。");
        RefreshUi();
    }
#endif

    public async void CreateRoom()
    {
        if (!lobbyReady || busy || inRoom)
        {
            return;
        }

        string roomName = $"{roomNamePrefix}-{UnityEngine.Random.Range(1000, 9999)}";
        await StartRoom(GameMode.Host, roomName);
    }

    public async void JoinRoom(string roomName)
    {
        if (!lobbyReady || busy || inRoom || string.IsNullOrWhiteSpace(roomName))
        {
            return;
        }

        await StartRoom(GameMode.Client, roomName);
    }

    public void StartGame()
    {
        if (!inRoom || runner == null || !runner.IsServer || gameStarted)
        {
            return;
        }

        int playerCount = runner.ActivePlayers.Count();
        if (playerCount < 1)
        {
            SetStatus("房間裡還沒有玩家。");
            return;
        }

        gameStarted = true;
        SetStatus("正在載入遊戲...");
        RefreshUi();
        runner.LoadScene(gameSceneName, LoadSceneMode.Single, LocalPhysicsMode.None, true);
    }

    public async void LeaveRoom()
    {
        if (isLeaving)
        {
            return;
        }

        isLeaving = true;
        busy = true;
        SetStatus("正在返回主選單...");
        RefreshUi();

        NetworkRunner leavingRunner = runner;
        runner = null;

        if (leavingRunner != null)
        {
            leavingRunner.RemoveCallbacks(this);
            await leavingRunner.Shutdown();
        }

        Destroy(gameObject);
        SceneManager.LoadScene(MainMenuSceneName);
    }

    public void RefreshRooms()
    {
        RefreshUi();
    }

    public void OnPlayerJoined(NetworkRunner networkRunner, PlayerRef player)
    {
        RefreshUi();

        if (gameStarted && networkRunner.IsServer)
        {
            SpawnPlayerIfNeeded(networkRunner, player);
        }
    }

    public void OnPlayerLeft(NetworkRunner networkRunner, PlayerRef player)
    {
        if (networkRunner.TryGetPlayerObject(player, out NetworkObject playerObject))
        {
            networkRunner.Despawn(playerObject);
        }

        ConfigureSinglePlayerCatAi(networkRunner);
        RefreshUi();
    }

    public void OnInput(NetworkRunner networkRunner, NetworkInput input)
    {
        UglyDogNetworkInput data = new UglyDogNetworkInput();

        if (gameStarted
            && !UpgradeShopUI.BlocksPlayerInput
            && !BuildShopUI.BlocksPlayerInput
            && !BuildingPlacementController.BlocksPlayerInput
            && !SettingsPanelUI.BlocksPlayerInput)
        {
            data.Move = UglyDogNetworkInput.ReadCameraRelativeMove();
            data.Buttons.Set((int)UglyDogInputButton.Attack, Input.GetKey(KeyCode.J));
        }

        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner networkRunner, PlayerRef player, NetworkInput input)
    {
        input.Set(default(UglyDogNetworkInput));
    }

    public void OnSessionListUpdated(NetworkRunner networkRunner, List<SessionInfo> sessionList)
    {
        if (!lobbyReady)
        {
            return;
        }

        sessions.Clear();
        sessions.AddRange(sessionList.Where(session => session.IsOpen && session.PlayerCount < session.MaxPlayers));
        RefreshUi();
    }

    public void OnSceneLoadDone(NetworkRunner networkRunner)
    {
        if (!gameStarted && !inRoom)
        {
            return;
        }

        PrepareGameplayScene();

        if (!networkRunner.IsServer)
        {
            return;
        }

        foreach (PlayerRef player in networkRunner.ActivePlayers)
        {
            SpawnPlayerIfNeeded(networkRunner, player);
        }

        ConfigureSinglePlayerCatAi(networkRunner);
    }

    private void PrepareGameplayScene()
    {
        gameStarted = true;

        if (gameplayScenePrepared)
        {
            return;
        }

        gameplayScenePrepared = true;
        HideLobbyUiForGame();
        EnsureGameplaySceneIsVisible();
        StartCoroutine(EnsureVisiblePlayerAfterLoad());
    }

    private async System.Threading.Tasks.Task StartRoom(GameMode mode, string roomName)
    {
        busy = true;
        SetStatus(mode == GameMode.Host ? $"正在建立房間 {roomName}..." : $"正在加入房間 {roomName}...");
        RefreshUi();

        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = roomName,
            PlayerCount = MaxPlayersPerRoom,
            SceneManager = sceneManager,
            ObjectProvider = objectProvider
        });

        if (isLeaving || this == null)
        {
            return;
        }

        busy = false;
        inRoom = result.Ok;

        if (!result.Ok)
        {
            SetStatus($"房間操作失敗：{result.ShutdownReason}");
            RefreshUi();
            return;
        }

        SetStatus(runner.IsServer ? "房間已建立。可以開始遊戲，或等待第二位玩家。" : "已加入房間。等待房主開始遊戲。");
        RefreshUi();
    }

    private void SpawnPlayerIfNeeded(NetworkRunner networkRunner, PlayerRef player)
    {
        NetworkObject prefab = GetPrefabForPlayer(networkRunner, player);
        if (prefab == null)
        {
            Debug.LogError("UglyDogRoomLobby 需要 NetworkObject 玩家 prefab。");
            return;
        }

        if (networkRunner.TryGetPlayerObject(player, out _))
        {
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition(networkRunner, player);
        NetworkObject playerObject = networkRunner.Spawn(prefab, spawnPosition, Quaternion.identity, player);
        playerObject.gameObject.SetActive(true);
        networkRunner.SetPlayerObject(player, playerObject);

        ConfigureSinglePlayerCatAi(networkRunner);
    }

    private NetworkObject GetPrefabForPlayer(NetworkRunner networkRunner, PlayerRef player)
    {
        int playerIndex = GetPlayerIndex(networkRunner, player);
        if (playerIndex == 1 && secondPlayerPrefab != null)
        {
            return secondPlayerPrefab;
        }

        return playerPrefab;
    }

    private int GetPlayerIndex(NetworkRunner networkRunner, PlayerRef player)
    {
        int index = 0;
        foreach (PlayerRef activePlayer in networkRunner.ActivePlayers.OrderBy(active => active.RawEncoded))
        {
            if (activePlayer == player)
            {
                return index;
            }

            index++;
        }

        return Mathf.Max(0, player.RawEncoded - 1);
    }

    private IEnumerator EnsureVisiblePlayerAfterLoad()
    {
        for (int i = 0; i < 30; i++)
        {
            yield return null;

            CatPlayerController activePlayer = FindObjectOfType<CatPlayerController>();
            if (activePlayer != null && activePlayer.gameObject.activeInHierarchy)
            {
                yield break;
            }
        }

        CatPlayerController[] players = FindObjectsOfType<CatPlayerController>(true);
        foreach (CatPlayerController player in players)
        {
            if (player != null && (player.name.ToLowerInvariant().Contains("dog") || player.name.ToLowerInvariant().Contains("cat")))
            {
                player.gameObject.SetActive(true);
                break;
            }
        }
    }

    private Vector3 GetSpawnPosition(NetworkRunner networkRunner, PlayerRef player)
    {
        MinionTeam spawnTeam = GetPlayerIndex(networkRunner, player) == 1 ? MinionTeam.Cat : MinionTeam.Dog;
        if (TryGetShopRangeSpawn(spawnTeam, player, out Vector3 rangeSpawnPosition))
        {
            return rangeSpawnPosition;
        }

        if (spawnPositions == null || spawnPositions.Length == 0)
        {
            return Vector3.zero;
        }

        int index = Mathf.Abs(player.RawEncoded) % spawnPositions.Length;
        return spawnPositions[index];
    }

    private bool TryGetDogShopRangeSpawn(PlayerRef player, out Vector3 position)
    {
        return TryGetShopRangeSpawn(MinionTeam.Dog, player, out position);
    }

    private bool TryGetShopRangeSpawn(MinionTeam team, PlayerRef player, out Vector3 position)
    {
        position = Vector3.zero;

        GameObject shopRange = FindShopRange(team);
        if (shopRange == null)
        {
            return false;
        }

        Collider rangeCollider = shopRange.GetComponent<Collider>();
        if (rangeCollider == null)
        {
            return false;
        }

        Bounds bounds = rangeCollider.bounds;
        Vector3 candidate = GetShopRangeSpawnCandidate(team, shopRange, rangeCollider);
        position = ProjectToGround(candidate, bounds, shopRange.transform);
        return true;
    }

    private Vector3 GetShopRangeSpawnCandidate(MinionTeam team, GameObject shopRange, Collider rangeCollider)
    {
        Bounds bounds = rangeCollider.bounds;
        Vector3 direction = GetShopFrontDirection(team, shopRange);
        float radius = Mathf.Max(0.15f, Mathf.Min(bounds.extents.x, bounds.extents.z) - SpawnRangeMargin);
        Vector3 candidate = bounds.center + direction * radius * SpawnForwardWeight;
        candidate = ClampInsideBounds(candidate, bounds, SpawnRangeMargin);
        candidate = PullInsideCollider(rangeCollider, candidate);
        return candidate;
    }

    private Vector3 GetShopFrontDirection(MinionTeam team, GameObject shopRange)
    {
        MinionTeam enemyTeam = team == MinionTeam.Dog ? MinionTeam.Cat : MinionTeam.Dog;
        GameObject enemyRange = FindShopRange(enemyTeam);
        Vector3 direction = Vector3.zero;

        if (enemyRange != null && enemyRange != shopRange)
        {
            direction = enemyRange.transform.position - shopRange.transform.position;
        }

        if (direction.sqrMagnitude < 0.001f)
        {
            Transform shopRoot = FindShopRoot(team, shopRange);
            if (shopRoot != null)
            {
                direction = shopRange.transform.position - shopRoot.position;
            }
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = team == MinionTeam.Dog ? Vector3.forward : Vector3.back;
        }

        return direction.normalized;
    }

    private Transform FindShopRoot(MinionTeam team, GameObject shopRange)
    {
        string teamName = team == MinionTeam.Dog ? "dog" : "cat";
        Transform current = shopRange.transform;
        while (current != null)
        {
            string lowerName = current.name.ToLowerInvariant();
            if (lowerName.Contains(teamName) && lowerName.Contains("shop") && !lowerName.Contains("range"))
            {
                return current;
            }

            current = current.parent;
        }

        string[] names = team == MinionTeam.Dog
            ? new[] { "dogShop", "DogShop", "Dog Supply", "DOG SUPPLY" }
            : new[] { "catShop", "CatShop", "Cat Supply", "CAT SUPPLY" };

        for (int i = 0; i < names.Length; i++)
        {
            GameObject found = GameObject.Find(names[i]);
            if (found != null)
            {
                return found.transform;
            }
        }

        return shopRange.transform.parent;
    }

    private static Vector3 ClampInsideBounds(Vector3 candidate, Bounds bounds, float margin)
    {
        float xMargin = Mathf.Min(margin, Mathf.Max(0f, bounds.extents.x - 0.05f));
        float zMargin = Mathf.Min(margin, Mathf.Max(0f, bounds.extents.z - 0.05f));
        candidate.x = Mathf.Clamp(candidate.x, bounds.min.x + xMargin, bounds.max.x - xMargin);
        candidate.z = Mathf.Clamp(candidate.z, bounds.min.z + zMargin, bounds.max.z - zMargin);
        return candidate;
    }

    private static Vector3 PullInsideCollider(Collider rangeCollider, Vector3 candidate)
    {
        Vector3 center = rangeCollider.bounds.center;
        center.y = candidate.y;

        for (int i = 0; i < 8; i++)
        {
            Vector3 closest = rangeCollider.ClosestPoint(candidate);
            if ((closest - candidate).sqrMagnitude < 0.0001f)
            {
                return candidate;
            }

            candidate = Vector3.Lerp(candidate, center, 0.45f);
        }

        return center;
    }

    private GameObject FindShopRange(MinionTeam team)
    {
        string[] names = team == MinionTeam.Dog
            ? new[] { "DogShopRange", "dogShopRange", "dogShop" }
            : new[] { "CatShopRange", "catShopRange", "catShop" };

        for (int i = 0; i < names.Length; i++)
        {
            GameObject found = GameObject.Find(names[i]);
            if (found != null)
            {
                return found;
            }
        }

        UpgradeShopZone[] zones = FindObjectsOfType<UpgradeShopZone>(true);
        string teamName = team == MinionTeam.Dog ? "dog" : "cat";
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null && zones[i].name.ToLowerInvariant().Contains(teamName))
            {
                return zones[i].gameObject;
            }
        }

        return null;
    }

    private void ConfigureSinglePlayerCatAi(NetworkRunner networkRunner)
    {
        if (!gameStarted || networkRunner == null || !networkRunner.IsServer)
        {
            return;
        }

        MinionManager manager = MinionManager.EnsureInstance();
        if (manager.GetComponent<MinionCatAiCommander>() == null)
        {
            manager.gameObject.AddComponent<MinionCatAiCommander>();
        }

        if (secondPlayerPrefab != null)
        {
            manager.SetSinglePlayerCatPrefab(secondPlayerPrefab.gameObject);
        }

        bool hasHumanCat = networkRunner.ActivePlayers.Count() >= 2;
        manager.SetHumanCatOpponentPresent(hasHumanCat);

        if (!hasHumanCat)
        {
            manager.EnsureSinglePlayerCatStandIn();
        }
    }

    private Vector3 ProjectToGround(Vector3 candidate, Bounds bounds, Transform ignoredRoot)
    {
        Vector3 rayOrigin = candidate + Vector3.up * Mathf.Max(8f, bounds.extents.y + 2f);
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);
        bool foundNamedGround = false;
        bool foundAnyGround = false;
        float bestNamedGroundY = float.PositiveInfinity;
        float bestAnyGroundY = float.PositiveInfinity;
        Vector3 bestNamedGroundPoint = candidate;
        Vector3 bestAnyGroundPoint = candidate;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hits[i].normal.y < 0.5f)
            {
                continue;
            }

            if (IsIgnoredSpawnSurface(hitCollider, ignoredRoot))
            {
                continue;
            }

            float hitY = hits[i].point.y;
            if (HasGroundSurfaceName(hitCollider))
            {
                if (!foundNamedGround || hitY < bestNamedGroundY)
                {
                    foundNamedGround = true;
                    bestNamedGroundY = hitY;
                    bestNamedGroundPoint = hits[i].point;
                }

                continue;
            }

            if (!foundAnyGround || hitY < bestAnyGroundY)
            {
                foundAnyGround = true;
                bestAnyGroundY = hitY;
                bestAnyGroundPoint = hits[i].point;
            }
        }

        if (foundNamedGround)
        {
            return bestNamedGroundPoint + Vector3.up * 0.03f;
        }

        if (foundAnyGround)
        {
            return bestAnyGroundPoint + Vector3.up * 0.03f;
        }

        return new Vector3(candidate.x, bounds.min.y + 0.03f, candidate.z);
    }

    private static bool IsIgnoredSpawnSurface(Collider hitCollider, Transform ignoredRoot)
    {
        if (ignoredRoot != null
            && (hitCollider.transform.IsChildOf(ignoredRoot) || ignoredRoot.IsChildOf(hitCollider.transform)))
        {
            return true;
        }

        Transform current = hitCollider.transform;
        while (current != null)
        {
            string lowerName = current.name.ToLowerInvariant();
            if (lowerName.Contains("roof")
                || lowerName.Contains("shop")
                || lowerName.Contains("range"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool HasGroundSurfaceName(Collider hitCollider)
    {
        Transform current = hitCollider.transform;
        while (current != null)
        {
            string lowerName = current.name.ToLowerInvariant();
            if (lowerName.Contains("ground")
                || lowerName.Contains("floor")
                || lowerName.Contains("terrain")
                || lowerName.Contains("grass")
                || lowerName.Contains("path"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void EnsureGameplaySceneIsVisible()
    {
        foreach (Camera camera in FindObjectsOfType<Camera>(true))
        {
            if (camera == null)
            {
                continue;
            }

            if (camera.gameObject.scene.name == gameSceneName)
            {
                camera.gameObject.SetActive(true);
                camera.enabled = true;
                camera.tag = "MainCamera";
            }
        }

        foreach (Light light in FindObjectsOfType<Light>(true))
        {
            if (light != null && light.gameObject.scene.name == gameSceneName)
            {
                light.gameObject.SetActive(true);
                light.enabled = true;
            }
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.42f, 0.47f, 0.55f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.30f, 0.36f, 0.30f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.20f, 0.18f, 0.16f, 1f);
        RenderSettings.ambientIntensity = 1.15f;
        RenderSettings.fog = false;
    }

    private void DisableLobbyCamera()
    {
        if (lobbyCamera != null)
        {
            lobbyCamera.gameObject.SetActive(false);
        }
    }

    private void HideLobbyUiForGame()
    {
        if (canvas == null)
        {
            Transform existingCanvas = transform.Find("Room Lobby Canvas");
            if (existingCanvas != null)
            {
                canvas = existingCanvas.GetComponent<Canvas>();
            }
        }

        if (canvas != null)
        {
            Destroy(canvas.gameObject);
            canvas = null;
        }

        DisableLobbyCamera();
    }

    private void BuildUi()
    {
        EnsureEventSystem();
        EnsureCamera();

        Transform existingCanvas = transform.Find("Room Lobby Canvas");
        if (existingCanvas != null && BindExistingUi(existingCanvas.gameObject))
        {
            return;
        }

        GameObject canvasObject = new GameObject("Room Lobby Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        ConfigureCanvasBackground(canvasObject);

        RectTransform root = CreatePanel(canvasObject.transform, "Panel", new Vector2(760f, 620f), new Vector2(0.5f, 0.5f));
        root.anchoredPosition = Vector2.zero;

        titleText = CreateText(root, "Title", "房間大廳", LobbyFontSize, TextAnchor.MiddleCenter);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, 250f);
        titleText.rectTransform.sizeDelta = new Vector2(700f, 60f);

        statusText = CreateText(root, "Status", string.Empty, LobbyFontSize, TextAnchor.MiddleCenter);
        statusText.rectTransform.anchoredPosition = new Vector2(0f, 200f);
        statusText.rectTransform.sizeDelta = new Vector2(700f, 54f);

        roomListRoot = CreatePanel(root, "Room List", new Vector2(700f, 300f), new Vector2(0.5f, 0.5f));
        roomListRoot.anchoredPosition = new Vector2(0f, 20f);

        refreshButton = CreateButton(root, "Refresh Button", "更新", new Vector2(-245f, -180f), RefreshRooms);
        createRoomButton = CreateButton(root, "Create Room Button", "建立房間", new Vector2(0f, -180f), CreateRoom);
        startGameButton = CreateButton(root, "Start Game Button", "開始遊戲", new Vector2(245f, -180f), StartGame);
        leaveButton = CreateTopLeftButton(canvasObject.transform, "Back Button", "返回", LeaveRoom);
    }

    private bool BindExistingUi(GameObject canvasObject)
    {
        canvas = canvasObject.GetComponent<Canvas>();
        titleText = FindChildComponent<Text>(canvasObject.transform, "Title");
        statusText = FindChildComponent<Text>(canvasObject.transform, "Status");
        roomListRoot = FindChildComponent<RectTransform>(canvasObject.transform, "Room List");
        refreshButton = FindChildComponent<Button>(canvasObject.transform, "Refresh Button");
        createRoomButton = FindChildComponent<Button>(canvasObject.transform, "Create Room Button");
        startGameButton = FindChildComponent<Button>(canvasObject.transform, "Start Game Button");
        leaveButton = FindChildComponent<Button>(canvasObject.transform, "Back Button");

        if (canvas == null || titleText == null || statusText == null || roomListRoot == null
            || refreshButton == null || createRoomButton == null || startGameButton == null || leaveButton == null)
        {
            return false;
        }

        ConfigureCanvasBackground(canvasObject);
        ApplyChineseLobbyLabels();
        refreshButton.onClick.RemoveAllListeners();
        refreshButton.onClick.AddListener(RefreshRooms);
        createRoomButton.onClick.RemoveAllListeners();
        createRoomButton.onClick.AddListener(CreateRoom);
        startGameButton.onClick.RemoveAllListeners();
        startGameButton.onClick.AddListener(StartGame);
        leaveButton.onClick.RemoveAllListeners();
        leaveButton.onClick.AddListener(LeaveRoom);
        PinBackButtonToTopLeft(false);
        return true;
    }

    private void ApplyChineseLobbyLabels()
    {
        if (titleText != null)
        {
            titleText.text = "房間大廳";
        }

        SetButtonLabel(refreshButton, "更新");
        SetButtonLabel(createRoomButton, "建立房間");
        SetButtonLabel(startGameButton, "開始遊戲");
        SetButtonLabel(leaveButton, "返回");
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = label;
        }
    }

    private static void ConfigureCanvasBackground(GameObject canvasObject)
    {
        Image background = canvasObject.GetComponent<Image>();
        if (background != null)
        {
            background.color = Color.clear;
            background.raycastTarget = false;
        }
    }

    private static void ApplyLobbyTextSizing(Transform root)
    {
        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            text.fontSize = LobbyFontSize;
        }
    }

    private static T FindChildComponent<T>(Transform root, string childName) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
        {
            if (component.gameObject.name == childName)
            {
                return component;
            }
        }

        return null;
    }

    private RectTransform CreatePanel(Transform parent, string name, Vector2 size, Vector2 pivot)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.pivot = pivot;

        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.15f, 0.16f, 0.19f, 0.92f);
        return rect;
    }

    private Text CreateText(Transform parent, string name, string text, int fontSize, TextAnchor anchor)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        Text label = textObject.AddComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.alignment = anchor;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200f, 50f);
        rect.anchoredPosition = position;

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.45f, 0.85f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        buttonObject.AddComponent<ButtonHoverTint>();

        Text text = CreateText(buttonObject.transform, "Text", label, LobbyFontSize, TextAnchor.MiddleCenter);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    private Button CreateTopLeftButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(parent, name, label, Vector2.zero, action);
        leaveButton = button;
        PinBackButtonToTopLeft(true);
        return button;
    }

    private void PinBackButtonToTopLeft(bool applyDefaultSize)
    {
        if (canvas == null || leaveButton == null)
        {
            return;
        }

        RectTransform rect = leaveButton.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.SetAsLastSibling();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        if (applyDefaultSize)
        {
            rect.sizeDelta = new Vector2(150f, 44f);
        }

        rect.anchoredPosition = new Vector2(24f, -24f);
    }

    private void RefreshUi()
    {
        if (roomListRoot == null)
        {
            return;
        }

        for (int i = roomListRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = roomListRoot.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }

        if (!lobbyReady && !inRoom)
        {
            AddRoomListText("連線中...", 30f, LobbyFontSize);
        }
        else if (inRoom)
        {
            int playerCount = runner != null ? runner.ActivePlayers.Count() : 0;
            AddRoomListText($"目前房間：{runner.SessionInfo.Name}", 70f, LobbyFontSize);
            AddRoomListText($"玩家：{playerCount}/{MaxPlayersPerRoom}", 20f, LobbyFontSize);
            AddRoomListText(runner != null && runner.IsServer ? "你是房主。" : "等待房主開始。", -30f, LobbyFontSize);
        }
        else if (sessions.Count == 0)
        {
            AddRoomListText("目前沒有可加入的房間。", 30f, LobbyFontSize);
        }
        else
        {
            for (int i = 0; i < sessions.Count; i++)
            {
                SessionInfo session = sessions[i];
                AddRoomButton(session, 110f - i * 62f);
            }
        }

        refreshButton.interactable = lobbyReady && !busy && !inRoom;
        createRoomButton.interactable = lobbyReady && !busy && !inRoom;
        startGameButton.gameObject.SetActive(inRoom && runner != null && runner.IsServer);
        startGameButton.interactable = !busy && runner != null && runner.IsServer && runner.ActivePlayers.Count() >= 1;
        leaveButton.interactable = !isLeaving;
    }

    private void AddRoomButton(SessionInfo session, float y)
    {
        string label = $"{session.Name}   {session.PlayerCount}/{session.MaxPlayers}";
        Button button = CreateButton(roomListRoot, $"Room {session.Name}", label, new Vector2(0f, y), () => JoinRoom(session.Name));
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(620f, 48f);
    }

    private void AddRoomListText(string text, float y, int size)
    {
        Text label = CreateText(roomListRoot, "Room Text", text, size, TextAnchor.MiddleCenter);
        label.rectTransform.sizeDelta = new Vector2(620f, 48f);
        label.rectTransform.anchoredPosition = new Vector2(0f, y);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    private void EnsureCamera()
    {
        if (Camera.main != null || FindObjectOfType<Camera>() != null)
        {
            return;
        }

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        lobbyCamera = cameraObject.AddComponent<Camera>();
        lobbyCamera.clearFlags = CameraClearFlags.SolidColor;
        lobbyCamera.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        lobbyCamera.transform.position = new Vector3(0f, 0f, -10f);
    }

    public void OnObjectExitAOI(NetworkRunner networkRunner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner networkRunner, NetworkObject obj, PlayerRef player) { }
    public void OnShutdown(NetworkRunner networkRunner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner networkRunner) { }
    public void OnDisconnectedFromServer(NetworkRunner networkRunner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner networkRunner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner networkRunner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner networkRunner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner networkRunner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner networkRunner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner networkRunner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner networkRunner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner networkRunner)
    {
        if (gameStarted || inRoom)
        {
            gameStarted = true;
            HideLobbyUiForGame();
        }
    }
}
