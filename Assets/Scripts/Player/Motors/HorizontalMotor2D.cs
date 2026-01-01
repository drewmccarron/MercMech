using MercMech.Common;
using UnityEngine;

public class HorizontalMotor2D
{
    private readonly Rigidbody2D rb;

    [System.Serializable]
    public class Settings
    {
        [Header("Acceleration")]

        [Tooltip("Ground acceleration when moving. Higher = faster reach of target horizontal speed.\nSuggested range: 30 - 150")]
        public float groundAccel = 30f;      // ground acceleration when moving

        [Tooltip("Ground deceleration when no input. Higher = stops quicker when releasing input.\nSuggested range: 30 - 150")]
        public float groundDecel = 15f;      // ground deceleration when no input

        [Tooltip("Ground reverse acceleration (turning). Higher = faster reversal of direction on ground.\nSuggested range: 60 - 200")]
        public float groundTurnAccel = 30f; // ground reverse accel

        [Tooltip("Air control acceleration when player provides horizontal input. Higher = stronger air steering.\nSuggested range: 10 - 80")]
        public float airAccel = 15f;         // air control acceleration

        [Tooltip("Air drag / deceleration when no horizontal input. Higher = faster slowdown in air.\nSuggested range: 5 - 40")]
        public float airDecel = 5f;         // air drag when no input

        [Tooltip("Air reverse acceleration (turning) while airborne. Higher = faster air reversals.\nSuggested range: 20 - 120")]
        public float airTurnAccel = 25f;     // air reverse accel
    }

    [System.Serializable]
    public class MoveSettings
    {
        [Header("Move")]

        [Tooltip("Max horizontal move speed when unboosted and on ground (walking).\nSuggested range: 2 - 5")]
        public float maxUnboostedGroundSpeed = 4f;

        [Tooltip("Max horizontal move speed when boosted/sprinting on ground.\nSuggested range: 6 - 12")]
        public float maxGroundBoostSpeed = 8f;

        [Header("Air Horizontal Caps")]
        [Tooltip("Maximum horizontal speed while falling (not flying). This is a hard cap applied in-air when the player is not in flight.\nSuggested range: 2 - 6")]
        public float maxFallingHorizontalSpeed = 3f;

        [Tooltip("Maximum horizontal speed while flying. This cap is applied during flight (can be higher than falling cap).\nSuggested range: 3 - 8")]
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
    public void ProcessHorizontalMovement(bool groundedNow, float moveInputDirection, bool boostHeld, float qbFlyCarryTimer, float qbCarryVx, bool isFlying)
    {
        float maxSpeed = CurrentMaxHorizontalMoveSpeed(boostHeld, groundedNow, isFlying);
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
                if (carryDir > 0)
                    targetVelocity = Mathf.Max(targetVelocity, qbCarryVx);
                else if (carryDir < 0)
                    targetVelocity = Mathf.Min(targetVelocity, qbCarryVx);
            }
        }

        bool hasInput = Mathf.Abs(moveInputDirection) > 0.001f;
        bool reversing = hasInput &&
                             Mathf.Sign(targetVelocity) != Mathf.Sign(currentVelocity) &&
                             Mathf.Abs(currentVelocity) > 0.1f;
        if (groundedNow)
        {
            rb.linearVelocity = new Vector2(getGroundVelocity(hasInput, reversing, currentVelocity, targetVelocity, dt), rb.linearVelocity.y);
        }
        else
        {
            // Air: apply thrust while input, otherwise apply drag.
            if (hasInput)
            {
                rb.linearVelocity = new Vector2(getAirVelocity(reversing, moveInputDirection, maxSpeed), rb.linearVelocity.y);
            }
            else
            {
                // No input: apply air drag toward 0.
                float newVx = getAirDragVelocity(dt);

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

    public float getGroundVelocity(bool hasInput, bool reversing, float currentVelocity, float targetVelocity, float dt)
    {
        // Grounded: deterministic MoveTowards-style acceleration & deceleration.
        float accelRate;
        if (!hasInput) accelRate = settings.groundDecel;
        else if (reversing) accelRate = settings.groundTurnAccel;
        else accelRate = settings.groundAccel;

        return Mathf.MoveTowards(currentVelocity, targetVelocity, accelRate * dt);
    }

    public float getAirVelocity(bool reversing, float moveInputDirection, float maxSpeed)
    {
        // Thrust amount. Use airTurnAccel when reversing, otherwise airAccel.
        float thrust = reversing ? settings.airTurnAccel : settings.airAccel;

        // Apply horizontal thrust (ForceMode2D.Force acts like "acceleration" for a given mass).
        rb.AddForce(Vector2.right * moveInputDirection * thrust, ForceMode2D.Force);

        // Optional: cap air top speed to your current maxSpeed (keeps things controllable).
        float vx = rb.linearVelocity.x;
        if (Mathf.Abs(vx) > maxSpeed)
            vx = Mathf.Sign(vx) * maxSpeed;

        return vx;
    }

    public float getAirDragVelocity(float dt)
    {
        float vx = rb.linearVelocity.x;
        return Mathf.MoveTowards(vx, 0f, settings.airDecel * dt);
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
