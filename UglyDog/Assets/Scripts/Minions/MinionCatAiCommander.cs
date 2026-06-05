using UnityEngine;

public class MinionCatAiCommander : MonoBehaviour
{
    [SerializeField] private bool onlyWhenNoHumanCatPlayer = true;
    [SerializeField] private float firstSummonDelay = 8f;
    [SerializeField] private float summonInterval = 10f;
    [SerializeField] private int rangedEveryNthSummon = 4;

    private float nextSummonTime;
    private float nextHumanCatCheckTime;
    private int summonCount;
    private bool cachedHasHumanCatPlayer;

    private void OnEnable()
    {
        nextSummonTime = Time.time + firstSummonDelay;
    }

    private void Update()
    {
        bool hasNetworkSession = UglyDogNetworkPlayer.HasRunningNetworkSession();
        if (hasNetworkSession && !UglyDogNetworkPlayer.IsStateSimulationPeer())
        {
            return;
        }

        MinionManager manager = MinionManager.EnsureInstance();
        if (!manager.ShouldRunSinglePlayerCatAi())
        {
            return;
        }

        if (Time.time < nextSummonTime)
        {
            return;
        }

        if (onlyWhenNoHumanCatPlayer && HasHumanCatPlayer())
        {
            nextSummonTime = Time.time + summonInterval;
            return;
        }

        nextSummonTime = Time.time + summonInterval;
        summonCount++;

        MinionKind kind = rangedEveryNthSummon > 0 && summonCount % rangedEveryNthSummon == 0
            ? MinionKind.Ranged
            : MinionKind.Melee;

        if (hasNetworkSession
            && UglyDogNetworkPlayer.TryBroadcastMinionSummon(kind, MinionTeam.Cat, Vector3.zero, false))
        {
            return;
        }

        manager.Summon(kind, MinionTeam.Cat);
    }

    private bool HasHumanCatPlayer()
    {
        if (Time.time < nextHumanCatCheckTime)
        {
            return cachedHasHumanCatPlayer;
        }

        nextHumanCatCheckTime = Time.time + 0.5f;
        CatPlayerController[] players = FindObjectsOfType<CatPlayerController>();
        for (int i = 0; i < players.Length; i++)
        {
            CatPlayerController player = players[i];
            if (PreferredPlayerFinder.IsPlayerTeam(player, MinionTeam.Cat) && player.HasRunningNetworkInputAuthority())
            {
                cachedHasHumanCatPlayer = true;
                return true;
            }
        }

        cachedHasHumanCatPlayer = false;
        return false;
    }
}
