using UnityEngine;

public class BarracksBuilding : MonoBehaviour
{
    [SerializeField] private KeyCode summonMeleeKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode summonRangedKey = KeyCode.Alpha2;
    [SerializeField] private float interactRadius = 3f;
    [SerializeField] private Vector3 promptLocalOffset = new Vector3(0f, 3.1f, 0f);

    private MinionTeam team;
    private float summonInterval = 12f;
    private float nextSummonTime;
    private WorldSpaceHealthLabel promptLabel;
    private CatPlayerController activePlayer;
    private string flashText;
    private float flashUntil;

    public void Configure(MinionTeam newTeam, float newSummonInterval)
    {
        team = newTeam;
        summonInterval = Mathf.Max(1f, newSummonInterval);
        nextSummonTime = Time.time + summonInterval;
    }

    private void Update()
    {
        UpdateAutoSummon();
        UpdatePlayerInteraction();
    }

    private void UpdateAutoSummon()
    {
        if (UglyDogNetworkPlayer.HasRunningNetworkSession() && !UglyDogNetworkPlayer.IsStateSimulationPeer())
        {
            return;
        }

        if (Time.time < nextSummonTime)
        {
            return;
        }

        nextSummonTime = Time.time + summonInterval;
        Summon(MinionKind.Melee);
    }

    private void UpdatePlayerInteraction()
    {
        CatPlayerController player = FindPlayerInRange();
        if (player == null)
        {
            activePlayer = null;
            HidePrompt();
            return;
        }

        activePlayer = player;
        ShowPrompt(player);

        if (Input.GetKeyDown(summonMeleeKey))
        {
            TryBuyAndSummon(MinionKind.Melee);
        }
        else if (Input.GetKeyDown(summonRangedKey))
        {
            TryBuyAndSummon(MinionKind.Ranged);
        }
    }

    private void TryBuyAndSummon(MinionKind kind)
    {
        MinionManager manager = MinionManager.EnsureInstance();
        Vector3 spawnPosition = GetSpawnPosition();
        UglyDogNetworkPlayer networkPlayer = activePlayer != null ? activePlayer.GetComponent<UglyDogNetworkPlayer>() : null;
        if (networkPlayer != null && activePlayer.HasRunningNetworkInputAuthority())
        {
            if (networkPlayer.RequestBuyMinion(kind, team, spawnPosition, true))
            {
                FlashPrompt("\u5df2\u53ec\u559a " + manager.GetDisplayName(kind));
            }
            else
            {
                FlashPrompt("\u9700\u8981 " + manager.GetCost(kind) + " \u91d1\u5e63");
            }

            return;
        }

        ResourceManager resources = ResourceManager.Instance;
        int cost = manager.GetCost(kind);
        if (resources == null || !resources.Spend(ResourceType.Coin, cost))
        {
            FlashPrompt("\u9700\u8981 " + cost + " \u91d1\u5e63");
            return;
        }

        Summon(kind);
        FlashPrompt("\u5df2\u53ec\u559a " + manager.GetDisplayName(kind));
    }

    private void Summon(MinionKind kind)
    {
        MinionManager manager = MinionManager.EnsureInstance();
        Vector3 spawnPosition = GetSpawnPosition();
        if (UglyDogNetworkPlayer.HasRunningNetworkSession()
            && UglyDogNetworkPlayer.IsStateSimulationPeer()
            && UglyDogNetworkPlayer.TryBroadcastMinionSummon(kind, team, spawnPosition, true))
        {
            return;
        }

        manager.SummonAt(kind, team, spawnPosition);
    }

    private Vector3 GetSpawnPosition()
    {
        return transform.position + transform.forward * 1.7f;
    }

    private CatPlayerController FindPlayerInRange()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, interactRadius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            CatPlayerController player = PreferredPlayerFinder.GetPreferredPlayer(hits[i]);
            if (player != null && player.enabled && player.gameObject.activeInHierarchy)
            {
                return player;
            }
        }

        return null;
    }

    private void ShowPrompt(CatPlayerController player)
    {
        if (promptLabel == null)
        {
            promptLabel = WorldSpaceHealthLabel.Create(
                player.transform,
                "Barracks Summon Prompt " + GetInstanceID(),
                promptLocalOffset,
                24,
                new Vector2(420f, 92f),
                0.01f);
        }
        else if (promptLabel.transform.parent != player.transform)
        {
            promptLabel.AttachTo(player.transform, promptLocalOffset);
        }

        promptLabel.gameObject.SetActive(true);
        promptLabel.SetText(GetPromptText());
    }

    private string GetPromptText()
    {
        if (Time.time < flashUntil && !string.IsNullOrEmpty(flashText))
        {
            return flashText;
        }

        MinionManager manager = MinionManager.EnsureInstance();
        return "1 " + manager.GetDisplayName(MinionKind.Melee) + " -" + manager.GetCost(MinionKind.Melee) + " \u91d1\u5e63"
            + "\n2 " + manager.GetDisplayName(MinionKind.Ranged) + " -" + manager.GetCost(MinionKind.Ranged) + " \u91d1\u5e63";
    }

    private void FlashPrompt(string text)
    {
        flashText = text;
        flashUntil = Time.time + 1.25f;
        if (promptLabel != null)
        {
            promptLabel.SetText(text);
        }
    }

    private void HidePrompt()
    {
        if (promptLabel != null)
        {
            promptLabel.gameObject.SetActive(false);
        }
    }
}
