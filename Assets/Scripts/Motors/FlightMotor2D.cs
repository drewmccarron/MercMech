using UnityEngine;

public class FlightMotor2D
{
    private readonly Rigidbody2D rb;
    private readonly Settings settings;

    [System.Serializable]
    public class Settings
    {
        [Header("Fly")]
        public float flyAcceleration = 30f;   // upward force while holding fly
        public float maxFlyUpSpeed = 4.5f;    // upward speed cap while flying
        public float flyGravityScale = 2f;    // gravity while flying
        public float normalGravityScale = 3f; // gravity when not flying
    }

    public FlightMotor2D(Rigidbody2D rb, Settings settings)
    {
        this.rb = rb;
        this.settings = settings;
    }

    // Flight logic separated from movement for easier tuning.
    public void ProcessFlight(bool anyFlyInputHeld, ref bool jumpedFromGround)
    {
        // If the player jumped from ground and is still rising, block flight until apex.
        bool isInJumpRisePhase = jumpedFromGround && rb.linearVelocity.y > 0f;
        bool shouldFlyNow = !isInJumpRisePhase && anyFlyInputHeld;

        if (shouldFlyNow)
        {
            TryFly();
            jumpedFromGround = false; // once flying, clear the gate
        }
        else
        {
            rb.gravityScale = settings.normalGravityScale;
        }
    }

    // Apply flying forces & gravity changes.
    private void TryFly()
    {
        rb.gravityScale = settings.flyGravityScale;

        // apply upward acceleration
        rb.AddForce(Vector2.up * settings.flyAcceleration, ForceMode2D.Force);

        // cap upward speed
        if (rb.linearVelocity.y > settings.maxFlyUpSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, settings.maxFlyUpSpeed);
    }
}
