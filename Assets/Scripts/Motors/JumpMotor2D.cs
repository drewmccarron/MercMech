using UnityEngine;

public class JumpMotor2D
{
    private readonly Rigidbody2D rb;
    private readonly Settings settings;

    // Jump state (kept same names / intent)
    public bool jumpedFromGround; // gates flight until apex after a ground jump
    private float timeSinceLastGrounded;

    // Jump assist
    private float jumpBufferTimer;

    [System.Serializable]
    public class Settings
    {
        [Header("Jump")]
        public float jumpForce = 10f;
        public LayerMask groundLayer;
        public float coyoteTime = 0.1f; // allow jump shortly after leaving ground

        [Header("Jump Assist")]
        public float jumpBufferTime = 0.12f; // buffer input before you land
    }

    public JumpMotor2D(Rigidbody2D rb, Settings settings)
    {
        this.rb = rb;
        this.settings = settings;
    }

    // Centralized fixed-timestep timer updates (mirrors your old TickFixedTimers logic).
    public void TickFixedTimers(float dt)
    {
        if (jumpBufferTimer > 0f)
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);
    }

    // Update grounded timers (mirrors your old UpdateGroundState behavior).
    public void UpdateGroundState(bool groundedNow, float dt)
    {
        if (groundedNow) timeSinceLastGrounded = 0f;
        else timeSinceLastGrounded += dt;
    }

    // Called from PlayerControls.OnJumpStarted
    public void OnJumpStarted()
    {
        jumpBufferTimer = settings.jumpBufferTime;

        bool canJump = timeSinceLastGrounded <= settings.coyoteTime && jumpBufferTimer > 0f;
        if (canJump)
        {
            PerformJump();

            // consume buffer + prevent immediate re-jump
            jumpBufferTimer = 0f;
            timeSinceLastGrounded = settings.coyoteTime + 1f;
        }
    }

    // Called from PlayerControls.OnJumpCanceled
    public void OnJumpCanceled()
    {
        jumpedFromGround = false;
    }

    // Execute the jump impulse and mark state to gate flight until apex.
    private void PerformJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, settings.jumpForce);
        jumpedFromGround = true;
    }
}
