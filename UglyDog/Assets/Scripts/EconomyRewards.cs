using UnityEngine;

public static class EconomyRewards
{
    public const int MinionKillCoins = 3;
    public const int BuildingDestroyedCoins = 30;

    public static void AwardLocalOpponentOf(MinionTeam defeatedTeam, int coins)
    {
        if (coins <= 0 || ResourceManager.Instance == null)
        {
            return;
        }

        MinionTeam rewardedTeam = GetOpponent(defeatedTeam);
        CatPlayerController localRewardedPlayer = PreferredPlayerFinder.FindPlayer(rewardedTeam);
        if (localRewardedPlayer == null)
        {
            return;
        }

        ResourceManager.Instance.Add(ResourceType.Coin, coins);
    }

    private static MinionTeam GetOpponent(MinionTeam team)
    {
        return team == MinionTeam.Dog ? MinionTeam.Cat : MinionTeam.Dog;
    }
}
