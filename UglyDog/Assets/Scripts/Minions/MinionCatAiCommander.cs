using UnityEngine;

public class MinionCatAiCommander : MonoBehaviour
{
    [SerializeField] private bool onlyWhenNoHumanCatPlayer = true;
    [SerializeField] private float firstSummonDelay = 2.5f;
    [SerializeField] private float summonInterval = 5.5f;
    [SerializeField] private int rangedEveryNthSummon = 3;

    private float nextSummonTime;
    private int summonCount;

    private void OnEnable()
    {
        nextSummonTime = Time.time + firstSummonDelay;
    }

    private void Update()
    {
        MinionManager manager = MinionManager.EnsureInstance();
        if (!manager.ShouldRunSinglePlayerCatAi())
        {
            return;
        }

        if (onlyWhenNoHumanCatPlayer && HasHumanCatPlayer())
        {
            return;
        }

        if (Time.time < nextSummonTime)
        {
            return;
        }

        nextSummonTime = Time.time + summonInterval;
        summonCount++;

        MinionKind kind = rangedEveryNthSummon > 0 && summonCount % rangedEveryNthSummon == 0
            ? MinionKind.Ranged
            : MinionKind.Melee;

        manager.Summon(kind, MinionTeam.Cat);
    }

    private bool HasHumanCatPlayer()
    {
        CatPlayerController[] players = FindObjectsOfType<CatPlayerController>();
        for (int i = 0; i < players.Length; i++)
        {
            CatPlayerController player = players[i];
            if (PreferredPlayerFinder.IsPlayerTeam(player, MinionTeam.Cat) && player.HasRunningNetworkInputAuthority())
            {
                return true;
            }
        }

        return false;
    }
}
