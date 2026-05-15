using UnityEngine;

public static class MinionBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BindScene()
    {
        GameObject dogShopRange = GameObject.Find("DogShopRange");
        if (dogShopRange == null)
        {
            return;
        }

        MinionManager manager = MinionManager.EnsureInstance();
        if (UnityEngine.Object.FindObjectOfType<UglyDogRoomLobby>() != null)
        {
            return;
        }

        if (manager.GetComponent<MinionCatAiCommander>() == null)
        {
            manager.gameObject.AddComponent<MinionCatAiCommander>();
        }

        manager.EnsureSinglePlayerCatStandIn();
    }
}
