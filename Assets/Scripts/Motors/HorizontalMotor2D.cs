using MercMech.Common;
using UnityEngine;

public class HorizontalMotor2D
{
    private readonly Rigidbody2D rb;

    [System.Serializable]
    public class Settings
    {
        [Header("Acceleration")]
        public float groundAccel = 30f;      // ground acceleration when moving
        public float groundDecel = 15f;      // ground deceleration when no input
        public float groundTurnAccel = 30f; // ground reverse accel
        public float airAccel = 15f;         // air control acceleration
        public float airDecel = 5f;         // air drag when no input
        public float airTurnAccel = 25f;     // air reverse accel
    }

    [System.Serializable]
    public class MoveSettings
    {
        [Header("Move")]
        public float maxUnboostedGroundSpeed = 4f;
        public float maxGroundBoostSpeed = 8f;

        [Header("Air Horizontal Caps")]
        [Tooltip("Maximum horizontal speed while falling (not flying).")]
        public float maxFallingHorizontalSpeed = 3f;

        [Tooltip("Maximum horizontal speed while flying.")]
        public float maxFlyingHorizontalSpeed = 6f;
    }

    private readonly Settings settings;
    private readonly MoveSettings moveSettings;

    public HorizontalMotor2D(Rigidbody2D rb, Settings settings, MoveSettings moveSettings)
    {
        this.rb = rb;
        this.settings = settings;
        this.moveSettings = moveSettings;
    }

    // Horizontal movement separated for clarity and testing.
    public void ProcessHorizontalMovement(bool groundedNow, float moveInputDirection, bool boostHeld, float qbFlyCarryTimer, float qbCarryVx, bool inFlight)
    {
        float maxSpeed = CurrentMaxHorizontalMoveSpeed(boostHeld, groundedNow, inFlight);
        float targetVelocity = moveInputDirection * maxSpeed;
        float currentVelocity = rb.linearVelocity.x;
        float dt = Time.fixedDeltaTime;

        // Protect QB carry horizontal speed while carry timer active.
        bool protectCarry = qbFlyCarryTimer > 0f;
        int carryDir = InputUtils.AxisToDir(qbCarryVx);

        if (protectCarry)
        {
            int heldDirForCarry = InputUtils.AxisToDir(moveInputDirection);
            if (heldDirForCarry == 0 || heldDirForCarry == carryDir)
            {
                if (carryDir > 0) targetVelocity = Mathf.Max(targetVelocity, qbCarryVx);
                else if (carryDir < 0) targetVelocity = Mathf.Min(targetVelocity, qbCarryVx);
            }
        }

        bool hasInput = Mathf.Abs(moveInputDirection) > 0.001f;
        bool reversing = hasInput &&
                             Mathf.Sign(targetVelocity) != Mathf.Sign(currentVelocity) &&
                             Mathf.Abs(currentVelocity) > 0.1f;
        if (groundedNow)
        {
            // Grounded: deterministic MoveTowards-style acceleration & deceleration.
            float accelRate;
            if (!hasInput) accelRate = settings.groundDecel;
            else if (reversing) accelRate = settings.groundTurnAccel;
            else accelRate = settings.groundAccel;

            float newVx = Mathf.MoveTowards(currentVelocity, targetVelocity, accelRate * dt);
            rb.linearVelocity = new Vector2(newVx, rb.linearVelocity.y);
        }
        else
        {
            // Air: apply thrust while input, otherwise apply drag.
            if (hasInput)
            {
                // Thrust amount. Use airTurnAccel when reversing, otherwise airAccel.
                float thrust = reversing ? settings.airTurnAccel : settings.airAccel;

                // Apply horizontal thrust (ForceMode2D.Force acts like "acceleration" for a given mass).
                rb.AddForce(Vector2.right * moveInputDirection * thrust, ForceMode2D.Force);

                // Optional: cap air top speed to your current maxSpeed (keeps things controllable).
                float vx = rb.linearVelocity.x;
                if (Mathf.Abs(vx) > maxSpeed)
                    vx = Mathf.Sign(vx) * maxSpeed;

                rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
            }
            else
            {
                // No input: apply air drag toward 0.
                float vx = rb.linearVelocity.x;
                float newVx = Mathf.MoveTowards(vx, 0f, settings.airDecel * dt);

                // If QB carry protection is active, don't drag below the carried QB speed.
                if (protectCarry && carryDir != 0)
                {
                    if (carryDir > 0) newVx = Mathf.Max(newVx, qbCarryVx);
                    else newVx = Mathf.Min(newVx, qbCarryVx);
                }

                rb.linearVelocity = new Vector2(newVx, rb.linearVelocity.y);
            }
        }
    }

    // Provide a single place to decide the applicable horizontal cap.
    // Grounded: use walk/boost speeds. Air: use falling/flying caps (ignore boost while airborne).
    public float CurrentMaxHorizontalMoveSpeed(bool boostHeld, bool groundedNow, bool inFlight)
    {
        if (groundedNow)
            return boostHeld ? moveSettings.maxGroundBoostSpeed : moveSettings.maxUnboostedGroundSpeed;

        // In air: choose flying cap when inFlight, otherwise falling cap.
        return inFlight ? moveSettings.maxFlyingHorizontalSpeed : moveSettings.maxFallingHorizontalSpeed;
    }
}
