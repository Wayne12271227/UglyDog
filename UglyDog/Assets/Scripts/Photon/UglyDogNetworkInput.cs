using Fusion;
using UnityEngine;

public enum UglyDogInputButton
{
    Attack = 0
}

public struct UglyDogNetworkInput : INetworkInput
{
    public Vector2 Move;
    public NetworkButtons Buttons;
}
