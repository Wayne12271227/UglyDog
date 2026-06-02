using UnityEngine;

public static class PreferredPlayerFinder
{
    public const string PreferredPlayerName = "CAT";

    public static CatPlayerController FindPreferredPlayer()
    {
        CatPlayerController cat = FindPlayer(MinionTeam.Cat);
        if (cat != null)
        {
            return cat;
        }

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
            if (!IsLocallyUsable(candidate))
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

    public static CatPlayerController FindPreferredPlayer(System.Predicate<CatPlayerController> predicate)
    {
        CatPlayerController[] candidates = Object.FindObjectsOfType<CatPlayerController>();
        CatPlayerController preferred = null;
        CatPlayerController fallback = null;

        for (int i = 0; i < candidates.Length; i++)
        {
            CatPlayerController candidate = candidates[i];
            if (!IsLocallyUsable(candidate) || (predicate != null && !predicate(candidate)))
            {
                continue;
            }

            if (candidate.HasRunningNetworkInputAuthority())
            {
                return candidate;
            }

            if (preferred == null && IsPreferredPlayer(candidate))
            {
                preferred = candidate;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }
        }

        return preferred != null ? preferred : fallback;
    }

    public static CatPlayerController FindPlayer(MinionTeam team)
    {
        CatPlayerController[] candidates = Object.FindObjectsOfType<CatPlayerController>();
        CatPlayerController fallback = null;

        for (int i = 0; i < candidates.Length; i++)
        {
            CatPlayerController candidate = candidates[i];
            if (!IsLocallyUsable(candidate) || !IsPlayerTeam(candidate, team))
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
        if (!IsLocallyUsable(player))
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
        return IsLocallyUsable(player) && IsPlayerTeam(player, team) ? player : null;
    }

    private static bool IsLocallyUsable(CatPlayerController player)
    {
        return IsUsable(player) && player.HasLocalPlayerAuthority();
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
