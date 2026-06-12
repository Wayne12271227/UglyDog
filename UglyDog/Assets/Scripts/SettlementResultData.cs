using UnityEngine;

public static class SettlementResultData
{
    public static bool HasResult { get; private set; }
    public static MinionTeam WinningTeam { get; private set; } = MinionTeam.Dog;
    public static MinionTeam LosingTeam { get; private set; } = MinionTeam.Cat;
    public static float BattleDurationSeconds { get; private set; }
    public static GameObject WinnerPrefab { get; private set; }

    public static void SetResult(
        MinionTeam winningTeam,
        MinionTeam losingTeam,
        float battleDurationSeconds,
        GameObject winnerPrefab)
    {
        HasResult = true;
        WinningTeam = winningTeam;
        LosingTeam = losingTeam;
        BattleDurationSeconds = Mathf.Max(0f, battleDurationSeconds);
        WinnerPrefab = winnerPrefab;
    }
}
