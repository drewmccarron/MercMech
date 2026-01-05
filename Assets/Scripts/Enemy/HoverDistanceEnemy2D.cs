using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class HoverDistanceEnemy2D : MonoBehaviour
{
    [SerializeField] Transform player;

    [Header("Distance")]
    [SerializeField] float preferredHorizontalDistance = 6f;
    [SerializeField] float horizontalDeadband = 0.75f;

    [Header("Movement")]
    [SerializeField] float approachAccel = 20f;
    [SerializeField] float retreatAccel = 35f;
    [SerializeField] float maxSpeed = 8f;
    [SerializeField] float damping = 8f;

    [Header("Vertical Hover")]
    [SerializeField] float preferredVerticalOffset = 2f;
    [SerializeField] float verticalDeadband = 0.5f;
    [SerializeField] float verticalAccel = 18f;
    [SerializeField] float maxVerticalSpeed = 6f;

    [Header("Ground Clearance")]
    [SerializeField] float minGroundClearance = 7f;
    [SerializeField] GroundProbe2D.Settings groundProbeSettings = new GroundProbe2D.Settings();

    Rigidbody2D rb;
    Collider2D col;
    GroundProbe2D groundProbe;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        groundProbe = new GroundProbe2D(col, groundProbeSettings);
    }

    void Reset()
    {
        if (groundProbeSettings.groundLayer == 0)
            groundProbeSettings.groundLayer = LayerMask.GetMask("World");
    }

    void FixedUpdate()
    {
        if (!player) return;

        // Check if grounded
        bool isGrounded = groundProbe.Evaluate(rb, out var debugInfo);
        Vector2 toPlayer = (Vector2)player.position - rb.position;

        // --- Horizontal spacing ---
        float newVx = maintainanceHorizontalDistance(toPlayer);

        // --- Vertical hover ---
        float newVy = maintainanceVerticalOffset(toPlayer);

        rb.linearVelocity = new Vector2(newVx, newVy);
    }

    private float maintainanceHorizontalDistance(Vector2 toPlayer)
    {
        float absX = Mathf.Abs(toPlayer.x);
        float distanceFromEnemy = absX - preferredHorizontalDistance;

        float targetAccelX = 0f;

        // If outside deadband
        if (Mathf.Abs(distanceFromEnemy) > horizontalDeadband)
        {
            // If too far: accelerate toward player.
            // If too close: accelerate away from player.
            bool tooFar = distanceFromEnemy > 0f;

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
        return Mathf.Clamp(newVx, -maxSpeed, maxSpeed);
    }

    private float maintainanceVerticalOffset(Vector2 toPlayer)
    {
        // Determine target height
        float yError;
        float groundDistance = GetGroundDistance();

        // Priority 1: Maintain minimum ground clearance
        if (groundDistance < minGroundClearance)
        {
            // Too close to ground - error is how much we need to climb
            yError = minGroundClearance - groundDistance;
        }
        // Priority 2: Match player vertical offset
        else
        {
            // Safe height - use player relative positioning
            yError = toPlayer.y - preferredVerticalOffset;
        }

        float targetAccelY;
        if (Mathf.Abs(yError) > verticalDeadband)
        {
            targetAccelY = Mathf.Sign(yError) * verticalAccel;
        }
        else
        {
            targetAccelY = -rb.linearVelocity.y * damping;
        }

        float newVy = rb.linearVelocity.y + targetAccelY * Time.fixedDeltaTime;
        return Mathf.Clamp(newVy, -maxVerticalSpeed, maxVerticalSpeed);
    }

    private float GetGroundDistance()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            rb.position,
            Vector2.down,
            minGroundClearance,
            groundProbeSettings.groundLayer
        );

        if (hit.collider != null)
            return hit.distance;

        // No ground detected - assume safe distance
        return minGroundClearance;
    }

    void OnDrawGizmosSelected()
    {
        if (rb == null || col == null) return;

        // Draw ground probe visualization
        if (groundProbe != null && groundProbe.TryGetGroundProbeDebug(out var debugInfo))
        {
            Gizmos.color = debugInfo.grounded ? Color.red : Color.green;
            Gizmos.DrawWireCube(debugInfo.centerWorld, debugInfo.sizeWorld);
        }

        // Draw ground clearance check
        float groundDist = GetGroundDistance();
        Gizmos.color = Color.yellow;
        Vector2 start = rb.position;
        Vector2 end = start + Vector2.down * Mathf.Min(groundDist, minGroundClearance);
        Gizmos.DrawLine(start, end);

        // Draw minimum clearance threshold
        if (groundDist < minGroundClearance)
        {
            Gizmos.color = groundDist < minGroundClearance ? Color.red : Color.green;
            Vector2 targetHeight = start - Vector2.up * (groundDist - minGroundClearance);
            Gizmos.DrawWireSphere(targetHeight, 0.3f);
        }

        // Draw horizontal distance range
        if (player != null)
        {
            Gizmos.color = Color.cyan;
            Vector2 playerPos = player.position;
            float dir = Mathf.Sign(rb.position.x - playerPos.x);
            Vector2 closestPoint = playerPos + Vector2.right * dir * (preferredHorizontalDistance - horizontalDeadband);
            Vector2 farthestPoint = playerPos + Vector2.right * dir * (preferredHorizontalDistance + horizontalDeadband);

            Gizmos.DrawLine(closestPoint + Vector2.up * 5f, closestPoint + Vector2.down * 5f);
            Gizmos.DrawLine(farthestPoint + Vector2.up * 5f, farthestPoint + Vector2.down * 5f);
        }
    }
}