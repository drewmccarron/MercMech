using UnityEngine;

public class FlightMotor2D
{
    private readonly Rigidbody2D rb;
    private readonly Settings settings;

    // Tracks whether flight mode is currently active so we only change gravity when state transitions.
    internal bool flightActive;

    [System.Serializable]
    public class Settings
    {
        [Header("Fly")]
        public float flyAcceleration = 30f;   // upward force while holding fly
        public float maxFlyUpSpeed = 4.5f;    // upward speed cap while flying
        public float flyGravityScale = 2f;    // gravity while flying
        public float normalGravityScale = 3f; // gravity when not flying
        public float flyApexEngageVelocityThreshold = 2.0f;    // allow flight to engage once upward speed is <= this (jump -> flight transition)
    }

    public FlightMotor2D(Rigidbody2D rb, Settings settings)
    {
        this.rb = rb;
        this.settings = settings;
        flightActive = false;
    }

    // Flight logic separated from movement for easier tuning.
    // anyFlyInputHeld: true when fly or jump input is held.
    // jumpedFromGround: ref to gate used to block flight until apex after a ground jump.
    public void ProcessFlight(bool anyFlyInputHeld, ref bool jumpedFromGround)
    {
        // If the player jumped from ground and is still rising, block flight until apex.
        bool isInJumpRisePhase = jumpedFromGround && rb.linearVelocity.y > settings.flyApexEngageVelocityThreshold;
        bool shouldFlyNow = !isInJumpRisePhase && anyFlyInputHeld;

        if (shouldFlyNow)
        {
            // Enter flight state if not already active (only update gravity on state transition).
            if (!flightActive)
            {
                rb.gravityScale = settings.flyGravityScale;
                flightActive = true;
            }

            TryFly();

            // Once flight begins, clear the gate so apex-check won't block subsequent flight.
            jumpedFromGround = false;
        }
        else
        {
            // Only restore normal gravity if we were previously in flight.
            if (flightActive)
            {
                rb.gravityScale = settings.normalGravityScale;
                flightActive = false;
            }

            // If not flying and flight not active, do nothing (avoid setting gravity each FixedUpdate).
        }
    }

    // Apply flying forces & gravity changes.
    private void TryFly()
    {
        // apply upward acceleration
        rb.AddForce(Vector2.up * settings.flyAcceleration, ForceMode2D.Force);

        // cap upward speed
        if (rb.linearVelocity.y > settings.maxFlyUpSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, settings.maxFlyUpSpeed);
    }
}
