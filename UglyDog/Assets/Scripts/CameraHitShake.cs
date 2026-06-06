using UnityEngine;

public class CameraHitShake : MonoBehaviour
{
    private float remainingDuration;
    private float totalDuration;
    private float strength;
    private Vector3 lastOffset;

    public static void ShakeMainCamera(float duration, float shakeStrength)
    {
        if (duration <= 0f || shakeStrength <= 0f)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        CameraHitShake shaker = camera.GetComponent<CameraHitShake>();
        if (shaker == null)
        {
            shaker = camera.gameObject.AddComponent<CameraHitShake>();
        }

        shaker.Shake(duration, shakeStrength);
    }

    public void Shake(float duration, float shakeStrength)
    {
        remainingDuration = Mathf.Max(remainingDuration, duration);
        totalDuration = Mathf.Max(0.001f, duration);
        strength = Mathf.Max(strength, shakeStrength);
    }

    private void LateUpdate()
    {
        if (lastOffset.sqrMagnitude > 0f)
        {
            transform.position -= lastOffset;
            lastOffset = Vector3.zero;
        }

        if (remainingDuration <= 0f)
        {
            strength = 0f;
            return;
        }

        remainingDuration -= Time.unscaledDeltaTime;
        float fade = Mathf.Clamp01(remainingDuration / totalDuration);
        Vector2 shake = Random.insideUnitCircle * strength * fade;
        lastOffset = transform.right * shake.x + transform.up * shake.y;
        transform.position += lastOffset;
    }

    private void OnDisable()
    {
        if (lastOffset.sqrMagnitude > 0f)
        {
            transform.position -= lastOffset;
            lastOffset = Vector3.zero;
        }
    }
}
