using UnityEngine;

public class FlightMotor2D
{
    private readonly Rigidbody2D rb;
    private readonly Settings settings;

    // Tracks whether flight mode is currently active (only change gravity on transitions).
    private bool isFlying = false;
    private float flyThrottle01;

    // Expose read-only flight state
    public bool IsFlying => isFlying;
    public float FlyThrottle01 => flyThrottle01;

    [System.Serializable]
    public class Settings
    {
        [Header("Flight Mechanics")]

        [Tooltip("Upward acceleration applied while flying. Higher = faster climb.\nSuggested range: 20-50")]
        public float flyAcceleration = 30f;

        [Tooltip("Upward speed cap while flying. Limits vertical climb rate.\nSuggested range: 3-6")]
        public float maxFlyUpSpeed = 7f;

        [Tooltip("Gravity scale while flying (lower = floatier feel).\nSuggested range: 1.5-3.0")]
        public float flyGravityScale = 2f;

        [Tooltip("Gravity scale when not flying (normal physics).\nSuggested range: 2.5-4.0")]
        public float normalGravityScale = 3f;

        [Tooltip("Upward velocity threshold to block flight after ground jump (prevents instant fly after jump).\nSuggested range: 3-6")]
        public float flyUpwardEngageVelocityThreshold = 4.0f;

        [Tooltip("Time taken to reach max upward acceleration when activating flight (lower = snappier).\nSuggested range: 8-15")]
        public float thrustRampUpSpeed = 12f;     // how fast throttle reaches 1

        [Tooltip("Time taken to fall to zero upward acceleration when releasing flight (lower = drops faster).\nSuggested range: 12-20")]
        public float thrustRampDownSpeed = 18f;   // how fast throttle falls to 0

    }

    public FlightMotor2D(Rigidbody2D rb, Settings settings)
    {
        this.rb = rb;
        this.settings = settings;
    }

    // Process flight state and apply upward thrust. Respects jump-rise blocking and energy gates.
    public void ProcessFlight(bool anyFlyInputHeld, ref bool jumpedFromGround, bool hasEnergyForFlight, float dt)
    {
        // If player jumped from ground and is still rising fast, block flight until apex (prevents jump -> instant fly).
        bool isInJumpRisePhase = jumpedFromGround && rb.linearVelocity.y > settings.flyUpwardEngageVelocityThreshold;
        bool shouldFlyNow = !isInJumpRisePhase && anyFlyInputHeld && hasEnergyForFlight;

        if (shouldFlyNow)
        {
            // Only change gravity on transition (avoid redundant assignments).
            if (!isFlying)
            {
                rb.gravityScale = settings.flyGravityScale;
                isFlying = true;
            }

            ApplyFlyThrust(dt);

            // Clear jump gate once flight begins (apex-check no longer blocks).
            jumpedFromGround = false;
        }
        else
        {
            // Transition out of flight: restore normal gravity.
            if (isFlying)
            {
                rb.gravityScale = settings.normalGravityScale;
                isFlying = false;
            }
        }
    }

    // Apply upward thrust if below max fly speed cap.
    private void ApplyFlyThrust(float dt)
    {
        float targetThrottle = IsFlying ? 1f : 0f;

        float rampSpeed = IsFlying
            ? settings.thrustRampUpSpeed
            : settings.thrustRampDownSpeed;

        flyThrottle01 = Mathf.MoveTowards(
            flyThrottle01,
            targetThrottle,
            rampSpeed * dt
        );

        if (rb.linearVelocity.y < settings.maxFlyUpSpeed)
        {
            float accel = settings.flyAcceleration * flyThrottle01;
            rb.AddForce(Vector2.up * accel, ForceMode2D.Force);
        }
    }
}