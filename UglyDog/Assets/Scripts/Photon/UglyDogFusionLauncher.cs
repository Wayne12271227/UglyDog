using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class UglyDogFusionLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private NetworkObject secondPlayerPrefab;
    [SerializeField] private string sessionName = "UglyDog-Test-Room";
    [SerializeField] private Vector3[] spawnPositions =
    {
        new Vector3(-2f, 0f, -2f),
        new Vector3(2f, 0f, -2f),
        new Vector3(-2f, 0f, 2f),
        new Vector3(2f, 0f, 2f)
    };

    private NetworkRunner runner;

    private async void Start()
    {
        Application.runInBackground = true;

        if (FindObjectsOfType<NetworkRunner>().Any(existing => existing != null && existing != runner && existing.IsRunning))
        {
            enabled = false;
            return;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("UglyDogFusionLauncher needs a NetworkObject player prefab.");
            return;
        }

        runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        runner.AddCallbacks(this);

        NetworkSceneManagerDefault sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
        NetworkObjectProviderDefault objectProvider = gameObject.AddComponent<NetworkObjectProviderDefault>();

        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = sessionName,
            SceneManager = sceneManager,
            ObjectProvider = objectProvider
        });

        if (!result.Ok)
        {
            Debug.LogError($"Fusion start failed: {result.ShutdownReason}");
        }
    }

    public void OnPlayerJoined(NetworkRunner networkRunner, PlayerRef player)
    {
        if (!networkRunner.IsServer)
        {
            return;
        }

        NetworkObject prefab = GetPrefabForPlayer(networkRunner, player);
        if (prefab == null)
        {
            Debug.LogError("UglyDogFusionLauncher needs a NetworkObject player prefab.");
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition(networkRunner, player);
        NetworkObject playerObject = networkRunner.Spawn(prefab, spawnPosition, Quaternion.identity, player);
        networkRunner.SetPlayerObject(player, playerObject);
    }

    public void OnPlayerLeft(NetworkRunner networkRunner, PlayerRef player)
    {
        if (networkRunner.TryGetPlayerObject(player, out NetworkObject playerObject))
        {
            networkRunner.Despawn(playerObject);
        }
    }

    public void OnInput(NetworkRunner networkRunner, NetworkInput input)
    {
        UglyDogNetworkInput data = new UglyDogNetworkInput();

        if (!UpgradeShopUI.BlocksPlayerInput
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

    private Vector3 GetSpawnPosition(NetworkRunner networkRunner, PlayerRef player)
    {
        if (spawnPositions == null || spawnPositions.Length == 0)
        {
            return Vector3.zero;
        }

        int index = GetPlayerIndex(networkRunner, player) % spawnPositions.Length;
        return spawnPositions[index];
    }

    public void OnObjectExitAOI(NetworkRunner networkRunner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner networkRunner, NetworkObject obj, PlayerRef player) { }
    public void OnShutdown(NetworkRunner networkRunner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner networkRunner) { }
    public void OnDisconnectedFromServer(NetworkRunner networkRunner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner networkRunner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner networkRunner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner networkRunner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner networkRunner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner networkRunner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner networkRunner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner networkRunner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner networkRunner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner networkRunner) { }
    public void OnSceneLoadStart(NetworkRunner networkRunner) { }
}
