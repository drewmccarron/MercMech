using UnityEngine;

public class GroundProbe2D
{
    [System.Serializable]
    public class Settings
    {
        [Header("Ground Probe")]
        public LayerMask groundLayer;
    }

    // Cached collider
    private readonly Collider2D col;

    // Ground probing
    private ContactFilter2D groundFilter;
    private float groundProbeOffset = 0.01f;
    private Vector2 groundBoxOffset;

    // Reusable overlap buffer to avoid allocations
    private readonly Collider2D[] m_overlapResults = new Collider2D[1];

    public GroundProbe2D(Collider2D col, Settings settings)
    {
        this.col = col;

        if (settings == null)
            settings = new Settings();

        LayerMask groundLayer = settings.groundLayer;
        if (groundLayer == 0)
            groundLayer = LayerMask.GetMask("Ground");

        groundFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = groundLayer,
            useTriggers = false
        };

        // Small downward offset so OverlapBox doesn't intersect our own collider edge.
        groundBoxOffset = Vector2.down * groundProbeOffset;
    }

    // Ground probe: OverlapBox with a small downward offset to avoid false positives.
    public bool IsGrounded()
    {
        if (col == null) return false;

        Vector2 bottomCenterPoint =
            (Vector2)col.bounds.center + Vector2.down * (col.bounds.extents.y) + groundBoxOffset;

        Vector2 groundBoxSize = new Vector2(col.bounds.size.x * 0.9f, 0.08f);

        return Physics2D.OverlapBox(bottomCenterPoint, groundBoxSize, 0f, groundFilter, m_overlapResults) > 0;
    }
}
