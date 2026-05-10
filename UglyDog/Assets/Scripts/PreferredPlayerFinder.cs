using UnityEngine;

public static class PreferredPlayerFinder
{
    public const string PreferredPlayerName = "DOG";

    public static CatPlayerController FindPreferredPlayer()
    {
        CatPlayerController[] candidates = Object.FindObjectsOfType<CatPlayerController>();
        CatPlayerController fallback = null;

        for (int i = 0; i < candidates.Length; i++)
        {
            CatPlayerController candidate = candidates[i];
            if (!IsUsable(candidate))
            {
                continue;
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

    public static bool IsPreferredPlayer(CatPlayerController player)
    {
        return IsUsable(player) && NameContains(player.gameObject, PreferredPlayerName);
    }

    public static CatPlayerController GetPreferredPlayer(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        CatPlayerController player = other.GetComponentInParent<CatPlayerController>();
        return IsPreferredPlayer(player) ? player : null;
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
}
