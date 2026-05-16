using UnityEngine;

public static class PreferredPlayerFinder
{
    public const string PreferredPlayerName = "DOG";

    public static CatPlayerController FindPreferredPlayer()
    {
        CatPlayerController dog = FindPlayer(MinionTeam.Dog);
        if (dog != null)
        {
            return dog;
        }

        CatPlayerController[] candidates = Object.FindObjectsOfType<CatPlayerController>();
        CatPlayerController fallback = null;

        for (int i = 0; i < candidates.Length; i++)
        {
            CatPlayerController candidate = candidates[i];
            if (!IsUsable(candidate))
            {
                continue;
            }

            if (candidate.HasRunningNetworkInputAuthority())
            {
                return candidate;
            }

            if (IsPreferredPlayer(candidate))
            {
                return candidate;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }
        }

        return fallback;
    }

    public static CatPlayerController FindPlayer(MinionTeam team)
    {
        CatPlayerController[] candidates = Object.FindObjectsOfType<CatPlayerController>();
        CatPlayerController fallback = null;

        for (int i = 0; i < candidates.Length; i++)
        {
            CatPlayerController candidate = candidates[i];
            if (!IsUsable(candidate) || !IsPlayerTeam(candidate, team))
            {
                continue;
            }

            if (candidate.HasRunningNetworkInputAuthority())
            {
                return candidate;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }
        }

        return fallback;
    }

    public static bool IsPreferredPlayer(CatPlayerController player)
    {
        return IsUsable(player) && NameContains(player.gameObject, PreferredPlayerName);
    }

    public static bool IsPlayerTeam(CatPlayerController player, MinionTeam team)
    {
        if (!IsUsable(player))
        {
            return false;
        }

        string lowerName = GetHierarchyName(player.transform).ToLowerInvariant();
        if (team == MinionTeam.Cat)
        {
            return lowerName.Contains("cat");
        }

        return lowerName.Contains("dog") || !lowerName.Contains("cat");
    }

    public static CatPlayerController GetPreferredPlayer(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        CatPlayerController player = other.GetComponentInParent<CatPlayerController>();
        if (!IsUsable(player))
        {
            return null;
        }

        return player.HasRunningNetworkInputAuthority() || IsPreferredPlayer(player) ? player : null;
    }

    public static CatPlayerController GetPlayer(Collider other, MinionTeam team)
    {
        if (other == null)
        {
            return null;
        }

        CatPlayerController player = other.GetComponentInParent<CatPlayerController>();
        return IsPlayerTeam(player, team) ? player : null;
    }

    private static bool IsUsable(CatPlayerController player)
    {
        return player != null && player.gameObject.activeInHierarchy && player.enabled;
    }

    private static bool NameContains(GameObject target, string text)
    {
        return target != null
            && !string.IsNullOrWhiteSpace(text)
            && target.name.ToLowerInvariant().Contains(text.ToLowerInvariant());
    }

    private static string GetHierarchyName(Transform transform)
    {
        string names = string.Empty;
        Transform current = transform;
        while (current != null)
        {
            names += " " + current.name;
            current = current.parent;
        }

        return names;
    }
}
