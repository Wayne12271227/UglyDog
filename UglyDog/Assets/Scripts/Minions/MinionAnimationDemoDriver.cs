using UnityEngine;

[RequireComponent(typeof(MinionVisualAnimator))]
public class MinionAnimationDemoDriver : MonoBehaviour
{
    [SerializeField] private float walkDistance = 2.4f;
    [SerializeField] private float walkSpeed = 1.6f;
    [SerializeField] private float attackPause = 0.55f;

    private MinionVisualAnimator visualAnimator;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Vector3 laneForward;
    private int direction = 1;
    private float pauseTimer;
    private bool attackedThisPause;

    private void Awake()
    {
        visualAnimator = GetComponent<MinionVisualAnimator>();
        startPosition = transform.position;
        laneForward = transform.forward;
        targetPosition = startPosition + laneForward * walkDistance;
    }

    private void Update()
    {
        if (pauseTimer > 0f)
        {
            visualAnimator.SetMoving(false);
            pauseTimer -= Time.deltaTime;
            if (!attackedThisPause)
            {
                visualAnimator.PlayAttack();
                attackedThisPause = true;
            }

            return;
        }

        attackedThisPause = false;
        Vector3 destination = direction > 0 ? targetPosition : startPosition;
        Vector3 offset = destination - transform.position;
        offset.y = 0f;

        if (offset.sqrMagnitude <= 0.01f)
        {
            direction *= -1;
            pauseTimer = attackPause;
            transform.rotation = Quaternion.LookRotation(direction > 0 ? laneForward : -laneForward, Vector3.up);
            return;
        }

        Vector3 move = offset.normalized * walkSpeed * Time.deltaTime;
        if (move.sqrMagnitude > offset.sqrMagnitude)
        {
            move = offset;
        }

        transform.position += move;
        transform.rotation = Quaternion.LookRotation(offset.normalized, Vector3.up);
        visualAnimator.SetMoving(true, Mathf.InverseLerp(0.2f, 3f, walkSpeed));
    }
}
