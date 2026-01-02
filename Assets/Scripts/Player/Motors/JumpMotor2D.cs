using UnityEngine;

public class JumpMotor2D
{
    private readonly Rigidbody2D rb;
    private readonly Settings settings;

    // Jump state (kept same names / intent)
    public bool jumpedFromGround; // gates flight until apex after a ground jump

    // Windup state (new)
    private bool isWindingUp;
    private float windupTimer;
    private bool jumpKeyHeld;

    // Jump assist
    // Expose windup state so other systems (FlightMotor / PlayerControls) can block flight while wind-up is active.
    public bool IsWindingUp => isWindingUp;

    [System.Serializable]
    public class Settings
    {
        [Header("Jump")]

        [Tooltip("Vertical jump velocity applied when the jump activates.\nSuggested range: 6 - 16")]
        public float jumpForce = 10f;



        [Header("Jump Windup")]
        [Tooltip("Delay after press before jump activates when held (seconds). This creates the 'wind-up' feel.\nSuggested range: 0.1 - 1.0")]
        public float jumpWindupTime = 0.2f;
    }

    public JumpMotor2D(Rigidbody2D rb, Settings settings)
    {
        this.rb = rb;
        this.settings = settings;
    }

    // Centralized fixed-timestep timer updates (mirrors your old TickFixedTimers logic).
    public void TickFixedTimers(float dt)
    {
        if (isWindingUp)
        {
            windupTimer += dt;

            // When windup finishes, only perform the jump if the button is still held.
            if (windupTimer >= settings.jumpWindupTime)
            {
                if (jumpKeyHeld)
                    PerformJump();
                else
                    CancelWindup();
            }
        }
    }

    // Called from PlayerControls.OnJumpStarted
    public void OnJumpStarted()
    {
        jumpKeyHeld = true;

        // Start wind-up rather than instantly setting velocity.
        StartWindup();
    }

    // Called from PlayerControls.OnJumpCanceled
    public void OnJumpCanceled()
    {
        jumpKeyHeld = false;

        // If the player cancels during wind-up, cancel the wind-up entirely.
        if (isWindingUp)
        {
            CancelWindup();
            return;
        }

        // Otherwise clear the jumpedFromGround gate so flying can be attempted again.
        jumpedFromGround = false;
    }

    // Execute the jump impulse and mark state to gate flight until apex.
    private void PerformJump()
    {
        // Apply configured jump velocity
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, settings.jumpForce);
        jumpedFromGround = true;

        isWindingUp = false;
        windupTimer = 0f;
    }

    private void StartWindup()
    {
        // Reset timers & flags.
        isWindingUp = true;
        windupTimer = 0f;
    }

    private void CancelWindup()
    {
        isWindingUp = false;
        windupTimer = 0f;

        // Ensure jump gate is not set.
        jumpedFromGround = false;
    }
}
