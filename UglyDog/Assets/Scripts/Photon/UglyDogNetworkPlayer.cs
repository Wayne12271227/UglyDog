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
    private NetworkButtons previousButtons;
    private int observedActionSequence;

    [Networked] private float NetworkMoveAmount { get; set; }
    [Networked] private byte NetworkActionKind { get; set; }
    [Networked] private int NetworkActionSequence { get; set; }

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
            bool attackPressed = input.Buttons.WasPressed(previousButtons, (int)UglyDogInputButton.Attack);
            controller.ApplyNetworkInput(input.Move, attackPressed, Runner.DeltaTime);
            PublishMoveAmount(input.Move.magnitude);

            if (attackPressed)
            {
                PublishAction(UglyDogNetworkAction.Attack);
            }

            previousButtons = input.Buttons;
            return;
        }

        if (Object.HasStateAuthority)
        {
            controller.ApplyNetworkInput(Vector2.zero, false, Runner.DeltaTime);
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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestAction(byte actionKind)
    {
        PublishAction((UglyDogNetworkAction)actionKind);
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
