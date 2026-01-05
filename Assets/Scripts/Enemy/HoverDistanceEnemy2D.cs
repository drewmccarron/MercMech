using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class HoverDistanceEnemy2D : MonoBehaviour
{
    [SerializeField] Transform player;

    [Header("Distance")]
    [SerializeField] float preferredHorizontalDistance = 6f;
    [SerializeField] float horizontalHeadband = 0.75f;

    [Header("Movement")]
    [SerializeField] float approachAccel = 20f;
    [SerializeField] float retreatAccel = 35f;
    [SerializeField] float maxSpeed = 8f;
    [SerializeField] float damping = 8f;

    [Header("Optional vertical hover")]
    [SerializeField] bool maintainVerticalOffset = true;
    [SerializeField] float preferredVerticalOffset = 2f;
    [SerializeField] float verticalDeadband = 0.5f;
    [SerializeField] float verticalAccel = 18f;
    [SerializeField] float maxVerticalSpeed = 6f;

    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        if (!player) return;

        Vector2 toPlayer = (Vector2)player.position - rb.position;

        // --- Horizontal spacing ---
        float absX = Mathf.Abs(toPlayer.x);
        float distError = absX - preferredHorizontalDistance;

        float targetAccelX = 0f;

        if (Mathf.Abs(distError) > horizontalHeadband)
        {
            // Move towards correct distance.
            // If too far: accelerate toward player.
            // If too close: accelerate away from player.
            bool tooFar = distError > 0f;

            float dirTowardPlayer = Mathf.Sign(toPlayer.x); // +1 means player is right of enemy
            float moveDir = tooFar ? dirTowardPlayer : -dirTowardPlayer;

            float accel = tooFar ? approachAccel : retreatAccel;
            targetAccelX = moveDir * accel;
        }
        else
        {
            // Inside deadband: damp horizontal velocity to avoid drift/jitter
            targetAccelX = -rb.linearVelocity.x * damping;
        }

        float newVx = rb.linearVelocity.x + targetAccelX * Time.fixedDeltaTime;
        newVx = Mathf.Clamp(newVx, -maxSpeed, maxSpeed);

        // --- Vertical hover (optional) ---
        float newVy = rb.linearVelocity.y;
        if (maintainVerticalOffset)
        {
            float yError = toPlayer.y - preferredVerticalOffset;

            float targetAccelY;
            if (Mathf.Abs(yError) > verticalDeadband)
            {
                targetAccelY = Mathf.Sign(yError) * verticalAccel;
            }
            else
            {
                targetAccelY = -rb.linearVelocity.y * damping;
            }

            newVy = rb.linearVelocity.y + targetAccelY * Time.fixedDeltaTime;
            newVy = Mathf.Clamp(newVy, -maxVerticalSpeed, maxVerticalSpeed);
        }

        rb.linearVelocity = new Vector2(newVx, newVy);
    }
}
