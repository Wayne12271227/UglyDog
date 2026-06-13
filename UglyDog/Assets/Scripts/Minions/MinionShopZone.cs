using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MinionShopZone : MonoBehaviour
{
    [SerializeField] private KeyCode buyMeleeKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode buyRangedKey = KeyCode.Alpha2;
    [SerializeField] private bool requirePlayerController = true;
    [SerializeField] private Vector3 promptLocalOffset = new Vector3(0f, 2.25f, 0f);

    private Collider zoneCollider;
    private CatPlayerController activePlayer;
    private WorldSpaceHealthLabel promptLabel;
    private float nextInsideCheckTime;
    private string flashText;
    private float flashUntil;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;
    }

    private void Update()
    {
        if (!Application.isPlaying
            || BuildingPlacementController.BlocksPlayerInput
            || UpgradeShopUI.BlocksPlayerInput
            || BuildShopUI.BlocksPlayerInput
            || SettingsPanelUI.BlocksPlayerInput)
        {
            HidePrompt();
            return;
        }

        CatPlayerController player = GetPreferredPlayerInsideZone();
        if (player == null)
        {
            activePlayer = null;
            HidePrompt();
            return;
        }

        activePlayer = player;
        ShowPrompt(player);
        UpdatePrompt();

        if (Input.GetKeyDown(buyMeleeKey))
        {
            TryBuy(MinionKind.Melee);
        }
        else if (Input.GetKeyDown(buyRangedKey))
        {
            TryBuy(MinionKind.Ranged);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CatPlayerController player = GetPlayer(other);
        if (IsPlayer(player))
        {
            activePlayer = player;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CatPlayerController player = GetPlayer(other);
        if (player != null && player == activePlayer)
        {
            activePlayer = null;
            HidePrompt();
        }
    }

    private void TryBuy(MinionKind kind)
    {
        if (!MinionManager.IsPlayerManualSummonReady())
        {
            FlashPrompt(MinionManager.GetPlayerManualSummonCooldownText());
            return;
        }

        MinionManager manager = MinionManager.EnsureInstance();
        CatPlayerController buyer = GetPreferredPlayerInsideZone();
        UglyDogNetworkPlayer networkPlayer = buyer != null ? buyer.GetComponent<UglyDogNetworkPlayer>() : null;
        bool bought = networkPlayer != null && buyer.HasRunningNetworkInputAuthority()
            ? networkPlayer.RequestBuyMinion(kind, MinionTeam.Dog, Vector3.zero, false)
            : manager.TryBuyAndSummon(kind, MinionTeam.Dog);
        if (bought)
        {
            MinionManager.StartPlayerManualSummonCooldown();
        }

        FlashPrompt(bought ? GetBoughtText(kind) : GetNotEnoughCoinsText(kind));
    }

    private CatPlayerController GetPreferredPlayerInsideZone()
    {
        if (activePlayer != null && IsInsideZone(activePlayer.transform.position))
        {
            return activePlayer;
        }

        if (Time.time < nextInsideCheckTime)
        {
            return null;
        }

        nextInsideCheckTime = Time.time + 0.08f;
        CatPlayerController player = PreferredPlayerFinder.FindPreferredPlayer();
        if (player != null && IsInsideZone(player.transform.position))
        {
            return player;
        }

        return null;
    }

    private bool IsInsideZone(Vector3 worldPosition)
    {
        if (zoneCollider == null)
        {
            zoneCollider = GetComponent<Collider>();
        }

        Vector3 closest = zoneCollider.ClosestPoint(worldPosition);
        return (closest - worldPosition).sqrMagnitude <= 0.0001f;
    }

    private bool IsPlayer(CatPlayerController player)
    {
        return !requirePlayerController || player != null;
    }

    private CatPlayerController GetPlayer(Collider other)
    {
        return PreferredPlayerFinder.GetPreferredPlayer(other);
    }

    private void ShowPrompt(CatPlayerController player)
    {
        if (player == null)
        {
            return;
        }

        if (promptLabel == null)
        {
            promptLabel = WorldSpaceHealthLabel.Create(
                player.transform,
                "Minion Shop Prompt",
                promptLocalOffset,
                26,
                new Vector2(340f, 82f),
                0.01f);
        }
        else if (promptLabel.transform.parent != player.transform)
        {
            promptLabel.AttachTo(player.transform, promptLocalOffset);
        }

        promptLabel.gameObject.SetActive(true);
        promptLabel.SetText(Time.time < flashUntil ? flashText : GetPromptText());
    }

    private void HidePrompt()
    {
        if (promptLabel != null)
        {
            promptLabel.gameObject.SetActive(false);
        }
    }

    private void UpdatePrompt()
    {
        if (promptLabel == null || !promptLabel.gameObject.activeSelf || activePlayer == null)
        {
            return;
        }

        if (promptLabel.transform.parent != activePlayer.transform)
        {
            promptLabel.AttachTo(activePlayer.transform, promptLocalOffset);
        }

        promptLabel.SetText(Time.time < flashUntil ? flashText : GetPromptText());
    }

    private void FlashPrompt(string text)
    {
        flashText = text;
        flashUntil = Time.time + 1.1f;
        if (promptLabel != null)
        {
            promptLabel.SetText(text);
        }
    }

    private string GetPromptText()
    {
        MinionManager manager = MinionManager.EnsureInstance();
        return "1 " + manager.GetDisplayName(MinionKind.Melee) + " -" + manager.GetCost(MinionKind.Melee) + " \u91d1\u5e63"
            + "\n2 " + manager.GetDisplayName(MinionKind.Ranged) + " -" + manager.GetCost(MinionKind.Ranged) + " \u91d1\u5e63";
    }

    private string GetBoughtText(MinionKind kind)
    {
        return "\u5df2\u53ec\u559a " + MinionManager.EnsureInstance().GetDisplayName(kind);
    }

    private string GetNotEnoughCoinsText(MinionKind kind)
    {
        return "\u9700\u8981 " + MinionManager.EnsureInstance().GetCost(kind) + " \u91d1\u5e63";
    }
}
