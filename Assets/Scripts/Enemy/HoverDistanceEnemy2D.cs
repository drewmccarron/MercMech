using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class HoverDistanceEnemy2D : MonoBehaviour
{
    [SerializeField] GameObject player;

    [Header("Distance")]
    [SerializeField] float preferredHorizontalDistance = 6f;
    [SerializeField] float horizontalDeadband = 2.5f;

    [Header("Movement")]
    [SerializeField] float approachAccel = 12.5f;
    [SerializeField] float retreatAccel = 25f;
    [SerializeField] float maxSpeed = 8f;
    [SerializeField] float damping = 8f;

    [Header("Vertical Hover")]
    [SerializeField] float preferredVerticalOffset = 7f;
    [SerializeField] float verticalDeadband = 0.5f;
    [SerializeField] float verticalAccel = 15f;
    [SerializeField] float maxVerticalSpeed = 6f;

    [Header("Ground Clearance")]
    [SerializeField] float minGroundClearance = 7f;
    [SerializeField] GroundProbe2D.Settings groundProbeSettings = new GroundProbe2D.Settings();

    Rigidbody2D rb;
    Collider2D col;
    GroundProbe2D groundProbe;
    Transform playerTf;


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        groundProbe = new GroundProbe2D(col, groundProbeSettings);
        player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
            playerTf = player.transform;
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
        Vector2 toPlayer = (Vector2)playerTf.position - rb.position;

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
        float targetY = playerTf.position.y + preferredVerticalOffset;
        float targetVerticalOffset = targetY - rb.position.y;

        // Maintain minimum ground clearance
        float groundDistance = GetGroundDistance();
        if (groundDistance < minGroundClearance)
        {
            // Too close to ground - how much we need to climb
            targetVerticalOffset = Mathf.Max(targetVerticalOffset, minGroundClearance - groundDistance);
        }

        float targetAccelY;
        if (Mathf.Abs(targetVerticalOffset) > verticalDeadband)
        {
            targetAccelY = Mathf.Sign(targetVerticalOffset) * verticalAccel;
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
        // Cast from the bottom of the collider, not rb.position
        float bottomY = col.bounds.min.y;
        Vector2 origin = new Vector2(rb.position.x, bottomY);

        // Cast far enough to reliably find ground
        const float maxCast = 100f;

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            Vector2.down,
            maxCast,
            groundProbeSettings.groundLayer
        );

        if (hit.collider != null)
            return hit.distance;

        // No ground detected - treat as very far away
        return float.PositiveInfinity;
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
            Vector2 playerPos = playerTf.position;
            float dir = Mathf.Sign(rb.position.x - playerPos.x);
            Vector2 closestPoint = playerPos + Vector2.right * dir * (preferredHorizontalDistance - horizontalDeadband);
            Vector2 farthestPoint = playerPos + Vector2.right * dir * (preferredHorizontalDistance + horizontalDeadband);

            Gizmos.DrawLine(closestPoint + Vector2.up * 5f, closestPoint + Vector2.down * 5f);
            Gizmos.DrawLine(farthestPoint + Vector2.up * 5f, farthestPoint + Vector2.down * 5f);
        }
    }
}