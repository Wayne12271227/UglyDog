using Fusion;
using UnityEngine;

public enum UglyDogNetworkAction : byte
{
    None = 0,
    Attack = 1,
    Dig = 2,
    Build = 3,
    Stop = 4
}

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class UglyDogNetworkPlayer : NetworkBehaviour
{
    private CatPlayerController controller;
    private int observedActionSequence;

    [Networked] private float NetworkMoveAmount { get; set; }
    [Networked] private byte NetworkActionKind { get; set; }
    [Networked] private int NetworkActionSequence { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }

    public bool ShouldPredictBuildRequests => Object != null && Object.HasInputAuthority && !Object.HasStateAuthority;

    public override void Spawned()
    {
        gameObject.SetActive(true);
        SetVisibleAndInteractive(true);

        controller = GetComponent<CatPlayerController>();
        if (controller != null)
        {
            controller.SetNetworkControlled(true);
        }

        string characterName = gameObject.name.ToLowerInvariant().Contains("cat") ? "Cat" : "Dog";
        gameObject.name = Object.HasInputAuthority ? $"Local Network {characterName}" : $"Remote Network {characterName}";

        if (Object.HasInputAuthority)
        {
            AssignLocalCamera();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (controller != null)
        {
            controller.SetNetworkControlled(false);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (controller == null)
        {
            return;
        }

        if (GetInput(out UglyDogNetworkInput input))
        {
            bool attackPressed = input.Buttons.WasPressed(PreviousButtons, (int)UglyDogInputButton.Attack);
            bool allowLocalSideEffects = Object.HasInputAuthority && Runner.IsForward;
            bool allowGameplayEffects = Object.HasStateAuthority;
            controller.ApplyNetworkInput(input.Move, attackPressed, Runner.DeltaTime, allowLocalSideEffects, allowGameplayEffects);
            PublishMoveAmount(input.Move.magnitude);

            if (attackPressed)
            {
                PublishAction(UglyDogNetworkAction.Attack);
            }

            PreviousButtons = input.Buttons;
            return;
        }

        if (Object.HasStateAuthority)
        {
            controller.ApplyNetworkInput(Vector2.zero, false, Runner.DeltaTime, false, true);
            PublishMoveAmount(0f);
        }
    }

    public override void Render()
    {
        if (controller == null)
        {
            return;
        }

        if (!Object.HasInputAuthority && !Object.HasStateAuthority)
        {
            controller.ApplyNetworkAnimation(NetworkMoveAmount);
        }

        if (!Object.HasInputAuthority && observedActionSequence != NetworkActionSequence)
        {
            observedActionSequence = NetworkActionSequence;
            PlayPublishedAction((UglyDogNetworkAction)NetworkActionKind);
        }
    }

    public void RequestAction(UglyDogNetworkAction action)
    {
        if (Object == null || Runner == null)
        {
            return;
        }

        if (Object.HasStateAuthority)
        {
            PublishAction(action);
            return;
        }

        if (Object.HasInputAuthority)
        {
            RPC_RequestAction((byte)action);
        }
    }

    public bool RequestBuild(Vector3 zoneAnchorPosition, BuildSiteBuildingType buildingType, MinionTeam requestedTeam)
    {
        if (Object == null || Runner == null)
        {
            return false;
        }

        if (Object.HasStateAuthority)
        {
            TryBuildOnStateAuthority(zoneAnchorPosition, (byte)buildingType, (byte)requestedTeam);
            return true;
        }

        if (!Object.HasInputAuthority)
        {
            return false;
        }

        RPC_RequestBuild(zoneAnchorPosition, (byte)buildingType, (byte)requestedTeam);
        return true;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestAction(byte actionKind)
    {
        PublishAction((UglyDogNetworkAction)actionKind);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable, TickAligned = false)]
    private void RPC_RequestBuild(Vector3 zoneAnchorPosition, byte buildingType, byte requestedTeam)
    {
        TryBuildOnStateAuthority(zoneAnchorPosition, buildingType, requestedTeam);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable, TickAligned = false)]
    private void RPC_ApplyBuild(Vector3 zoneAnchorPosition, byte buildingType, byte team)
    {
        ArcherTowerBuildZone zone = ArcherTowerBuildZone.FindClosestNetworkZone(zoneAnchorPosition);
        if (zone == null)
        {
            return;
        }

        zone.TryCreateNetworkBuilding((BuildSiteBuildingType)buildingType, (MinionTeam)team);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, Channel = RpcChannel.Reliable, TickAligned = false)]
    private void RPC_RejectBuild(Vector3 zoneAnchorPosition, byte buildingType)
    {
        ArcherTowerBuildZone zone = ArcherTowerBuildZone.FindClosestNetworkZone(zoneAnchorPosition);
        if (zone != null)
        {
            zone.RejectNetworkBuildPrediction((BuildSiteBuildingType)buildingType);
        }

        ArcherTowerBuildZone.RefundBuildCost((BuildSiteBuildingType)buildingType);
    }

    private void TryBuildOnStateAuthority(Vector3 zoneAnchorPosition, byte buildingType, byte requestedTeam)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        ArcherTowerBuildZone zone = ArcherTowerBuildZone.FindClosestNetworkZone(zoneAnchorPosition);
        if (zone == null || zone.HasCurrentBuilding)
        {
            RPC_RejectBuild(zoneAnchorPosition, buildingType);
            return;
        }

        MinionTeam ownerTeam = GetAuthoritativeTeam((MinionTeam)requestedTeam);
        zone.TryCreateNetworkBuilding((BuildSiteBuildingType)buildingType, ownerTeam);
        RPC_ApplyBuild(zone.NetworkAnchorPosition, buildingType, (byte)ownerTeam);
    }

    private MinionTeam GetAuthoritativeTeam(MinionTeam fallbackTeam)
    {
        CatPlayerController localController = controller != null ? controller : GetComponent<CatPlayerController>();
        if (localController == null)
        {
            return fallbackTeam;
        }

        return PreferredPlayerFinder.IsPlayerTeam(localController, MinionTeam.Cat)
            ? MinionTeam.Cat
            : MinionTeam.Dog;
    }

    private void PublishMoveAmount(float moveAmount)
    {
        if (Object.HasStateAuthority)
        {
            NetworkMoveAmount = Mathf.Clamp01(moveAmount);
        }
    }

    private void PublishAction(UglyDogNetworkAction action)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        NetworkActionKind = (byte)action;
        NetworkActionSequence++;
    }

    private void PlayPublishedAction(UglyDogNetworkAction action)
    {
        switch (action)
        {
            case UglyDogNetworkAction.Attack:
                controller.PlayAttack();
                break;
            case UglyDogNetworkAction.Dig:
                controller.PlayDig();
                break;
            case UglyDogNetworkAction.Build:
                controller.PlayBuild();
                break;
            case UglyDogNetworkAction.Stop:
                controller.StopAction();
                break;
        }
    }

    private void SetVisibleAndInteractive(bool enabledState)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = enabledState;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = enabledState;
        }
    }

    private void AssignLocalCamera()
    {
        TopDownCameraFollow cameraFollow = FindObjectOfType<TopDownCameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.Target = transform;
            cameraFollow.SnapToTarget();
        }
    }
}
