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

    public static Vector2 ReadCameraRelativeMove()
    {
        Vector3 rawInput = ReadRawMoveInput();
        if (rawInput.sqrMagnitude <= 0.001f)
        {
            return Vector2.zero;
        }

        rawInput = Vector3.ClampMagnitude(rawInput, 1f);
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return new Vector2(rawInput.x, rawInput.z);
        }

        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 cameraRight = mainCamera.transform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;

        Vector3 worldMove = cameraForward.normalized * rawInput.z + cameraRight.normalized * rawInput.x;
        if (worldMove.sqrMagnitude <= 0.001f)
        {
            return Vector2.zero;
        }

        worldMove.Normalize();
        return new Vector2(worldMove.x, worldMove.z);
    }

    private static Vector3 ReadRawMoveInput()
    {
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 0.001f)
        {
            return input;
        }

        float horizontal = 0f;
        float vertical = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            horizontal -= 1f;
        }

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            horizontal += 1f;
        }

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            vertical -= 1f;
        }

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            vertical += 1f;
        }

        return new Vector3(horizontal, 0f, vertical);
    }
}
