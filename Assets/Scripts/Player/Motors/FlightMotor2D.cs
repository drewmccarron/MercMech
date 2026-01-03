using UnityEngine;

public class FlightMotor2D
{
    private readonly Rigidbody2D rb;
    private readonly Settings settings;

    // Tracks whether flight mode is currently active so we only change gravity when state transitions.
    private bool flightActive = false;
    private float flyThrottle01;

    // Expose read-only flight state
    public bool IsFlying => flightActive;

    [System.Serializable]
    public class Settings
    {
        [Header("Fly")]

        [Tooltip("Upward acceleration applied while flying. Higher = faster climb.\nSuggested range: 10 - 80")]
        public float flyAcceleration = 30f;

        [Tooltip("Upward speed cap while flying.\nSuggested range: 2 - 8")]
        public float maxFlyUpSpeed = 7f;

        [Tooltip("Gravity scale while flying (lower -> floatier).\nSuggested range: 0.5 - 3")]
        public float flyGravityScale = 2f;

        [Tooltip("Gravity scale normally (when not flying).\nSuggested range: 1 - 6")]
        public float normalGravityScale = 3f;

        [Tooltip("Upward velocity threshold used to block flight until achieved when jumping from ground.\nSuggested range: 0.5 - 6 (units/sec)")]
        public float flyUpwardEngageVelocityThreshold = 4.0f;

        [Tooltip("Time taken to reach max upward acceleration (not velocity)")]
        public float thrustRampUpSpeed = 12f;     // how fast throttle reaches 1

        [Tooltip("Time taken to fall to zero upward acceleration")]
        public float thrustRampDownSpeed = 18f;   // how fast throttle falls to 0

    }

    public FlightMotor2D(Rigidbody2D rb, Settings settings)
    {
        this.rb = rb;
        this.settings = settings;
    }

    // Flight logic separated from movement for easier tuning.
    // anyFlyInputHeld: true when fly or jump input is held.
    // jumpedFromGround: ref to gate used to block flight until apex after a ground jump.
    // hasEnergyForFlight: when false, flight is forced off (and upward velocity is cancelled for immediate drop).
    public void ProcessFlight(bool anyFlyInputHeld, ref bool jumpedFromGround, bool hasEnergyForFlight, float dt)
    {
        // If the player jumped from ground and is still rising, block flight until apex.
        bool isInJumpRisePhase = jumpedFromGround && rb.linearVelocity.y > settings.flyUpwardEngageVelocityThreshold;
        bool shouldFlyNow = !isInJumpRisePhase && anyFlyInputHeld && hasEnergyForFlight;

        if (shouldFlyNow)
        {
            // Only change gravity on transition.
            if (!flightActive)
            {
                rb.gravityScale = settings.flyGravityScale;
                flightActive = true;
            }

            TryFly(dt);

            // Once flight begins, clear the gate so apex-check won't block subsequent flight.
            jumpedFromGround = false;
        }
        else
        {
            // If we were flying but we no longer should fly, restore gravity once.
            if (flightActive)
            {
                rb.gravityScale = settings.normalGravityScale;
                flightActive = false;
            }

            // If not flying and flight not active, do nothing (avoid setting gravity each FixedUpdate).
        }
    }

    // Apply flying forces & gravity changes.
    private void TryFly(float dt)
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
