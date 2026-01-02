using UnityEngine;

public class GroundProbe2D
{
    [System.Serializable]
    public class Settings
    {
        [Header("Ground Probe")]
        public LayerMask groundLayer;

        [Tooltip("Vertical offset applied to the probe center (usually slightly downward).")]
        public Vector2 groundBoxOffset = new Vector2(0f, -0.01f);

        [Tooltip("Width multiplier relative to collider bounds.")]
        [Range(0.1f, 1f)] public float widthMultiplier = 0.9f;

        [Tooltip("Probe box height in world units.")]
        public float probeHeight = 0.08f;

        [Tooltip("Max number of contacts to capture for debug.")]
        public int maxContacts = 8;
    }

    public struct DebugInfo
    {
        public bool grounded;
        public Vector2 centerWorld;
        public Vector2 sizeWorld;
        public float angleDeg;

        public int contactCount;
        public ContactPoint2D[] contacts; // reference to internal buffer (no alloc)
    }

    private readonly Collider2D col;
    private readonly ContactFilter2D groundFilter;

    private readonly Collider2D[] overlapResults = new Collider2D[1];

    private readonly ContactPoint2D[] contactBuffer;
    private DebugInfo lastDebug;

    private readonly Settings settings;

    public GroundProbe2D(Collider2D col, Settings settings)
    {
        this.col = col;
        this.settings = settings ?? new Settings();

        groundFilter = new ContactFilter2D();
        groundFilter.useLayerMask = true;
        groundFilter.layerMask = this.settings.groundLayer;
        groundFilter.useTriggers = false;

        int n = Mathf.Max(1, this.settings.maxContacts);
        contactBuffer = new ContactPoint2D[n];

        lastDebug = new DebugInfo
        {
            grounded = false,
            contacts = contactBuffer
        };
    }

    /// <summary>
    /// Evaluate ground state. Also caches debug info that can be rendered.
    /// Pass rb if you want real contact normals/points.
    /// </summary>
    public bool Evaluate(Rigidbody2D rb, out DebugInfo info)
    {
        info = default;

        if (col == null)
        {
            lastDebug.grounded = false;
            lastDebug.contactCount = 0;
            info = lastDebug;
            return false;
        }

        Bounds b = col.bounds;

        Vector2 size = new Vector2(b.size.x * settings.widthMultiplier, settings.probeHeight);
        Vector2 center = new Vector2(b.center.x, b.min.y) + settings.groundBoxOffset;

        bool grounded =
            Physics2D.OverlapBox(center, size, 0f, groundFilter, overlapResults) > 0;

        int contactCount = 0;
        if (rb != null)
        {
            // Layer-filtered contact points (helps show normals)
            contactCount = rb.GetContacts(groundFilter, contactBuffer);
            if (contactCount > contactBuffer.Length) contactCount = contactBuffer.Length;
        }

        lastDebug.grounded = grounded;
        lastDebug.centerWorld = center;
        lastDebug.sizeWorld = size;
        lastDebug.angleDeg = 0f;
        lastDebug.contactCount = contactCount;
        lastDebug.contacts = contactBuffer;

        info = lastDebug;
        return grounded;
    }

    // Get the most recent debug info without re-evaluating
    public bool TryGetGroundProbeDebug(out DebugInfo debugInfo)
    {
        debugInfo = lastDebug;
        return col != null;
    }
}
