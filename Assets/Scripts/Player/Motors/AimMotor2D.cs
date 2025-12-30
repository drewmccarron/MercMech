using UnityEngine;

public class AimMotor2D
{
    // Current aim state (world space)
    public Vector2 AimWorldPosition { get; private set; }
    public Vector2 AimDirection { get; private set; }

    // Keep last non-zero direction so we don't NaN when cursor is exactly on origin.
    private Vector2 lastNonZeroDirection = Vector2.right;

    // Update aim from a screen-space pointer position.
    public void UpdateAim(Vector2 originWorld, Vector2 pointerScreenPos, Camera cam)
    {
        if (cam == null)
            return;

        // Convert pointer screen pos to world.
        // For ortho cameras, the Z doesn't really matter, but we set it safely anyway.
        Vector3 screen = new Vector3(pointerScreenPos.x, pointerScreenPos.y, -cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(screen);

        AimWorldPosition = new Vector2(world.x, world.y);

        Vector2 toAim = AimWorldPosition - originWorld;
        if (toAim.sqrMagnitude > 0.000001f)
        {
            lastNonZeroDirection = toAim.normalized;
        }

        AimDirection = lastNonZeroDirection;
    }

    // NEW: Update aim from a world-space target position (eg. enemy aiming at player).
    public void UpdateAimWorld(Vector2 originWorld, Vector2 targetWorld)
    {
        AimWorldPosition = targetWorld;

        Vector2 toAim = AimWorldPosition - originWorld;
        if (toAim.sqrMagnitude > 0.000001f)
        {
            lastNonZeroDirection = toAim.normalized;
        }

        AimDirection = lastNonZeroDirection;
    }

    // Convenience: compute a ray end point for a given length.
    public Vector2 RayEnd(Vector2 originWorld, float length)
    {
        return originWorld + AimDirection * length;
    }
}
