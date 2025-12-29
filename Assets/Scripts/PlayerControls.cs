using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControls : MonoBehaviour
{
    private Rigidbody2D rb;
    private Actions controls;
    private Collider2D col;
    private float moveInputDirection;

    [Header("Move")]
    public float walkSpeed = 5f;
    public float boostSpeed = 9f;

    [Header("Jump")]
    public float jumpForce = 10f;
    public float groundCheckDistance = 0.1f;
    public LayerMask groundLayer;

    [Header("Boost")]
    private bool boostHeld;

    [Header("Fly")]
    public float flyAcceleration = 30f;      // upward acceleration while holding Space
    public float maxFlyUpSpeed = 5f;         // cap upward speed
    public float flyGravityScale = 2f;     // gravity while flying
    public float normalGravityScale = 3f;    // gravity normally
    private bool flyHeld;

    [Header("Quick Boost")]
    public float quickBoostSpeed = 16f;     // initial burst speed
    public float quickBoostDuration = 0.35f; // seconds
    public AnimationCurve quickBoostCurve = null; // optional; if null we use a built-in ease
    public float quickBoostCooldown = 0.15f;

    private bool isQuickBoosting;
    private float quickBoostTimer;
    private float quickBoostCooldownTimer;
    private int quickBoostDir;              // -1 or +1
    private float quickBoostStartSpeed;

    public float quickBoostFlyExitUpVelocity = 10f; // tune: how much upward momentum to resume with

    [Header("Fall")]
    public float maxFallSpeed = 8f;

    // Facing direction: -1 = left, +1 = right
    private int facingDirection = 1;
    private bool wasFlyingBeforeQuickBoost;

    private float savedGravityScale;
    private float savedYVelocity;

    void Start()
    {

    }

    void Update()
    {
        // Left-Right Movement Input
        moveInputDirection = controls.Player.Walk.ReadValue<float>();

        // Update facing direction
        if (moveInputDirection > 0.2f) facingDirection = 1;
        else if (moveInputDirection < -0.2f) facingDirection = -1;

        // Quick Boost Cooldown
        if (quickBoostCooldownTimer > 0f)
            quickBoostCooldownTimer -= Time.deltaTime;
    }

    void Awake()
    {
        controls = new Actions();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.gravityScale = normalGravityScale;

        // Set default quick boost curve
        if (quickBoostCurve == null || quickBoostCurve.length == 0)
        {
            quickBoostCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.7f, 0.35f),
                new Keyframe(1f, 0f)
            );
        }

        // Set default ground layer
        if (groundLayer == 0)
        {
            groundLayer = LayerMask.GetMask("Ground");
        }
    }


    private void OnEnable()
    {
        controls.Player.Enable();

        // Jump
        controls.Player.Jump.performed += OnJump;

        // Boost
        controls.Player.GroundBoost.started += OnBoostStarted;
        controls.Player.GroundBoost.canceled += OnBoostCanceled;

        // Fly
        controls.Player.Fly.started += OnFlyStarted;
        controls.Player.Fly.canceled += OnFlyCanceled;

        // Quick Boost
        controls.Player.QuickBoost.performed += OnQuickBoost;
    }

    private void OnDisable()
    {
        controls.Player.Jump.performed -= OnJump;

        controls.Player.GroundBoost.started -= OnBoostStarted;
        controls.Player.GroundBoost.canceled -= OnBoostCanceled;

        controls.Player.Fly.started -= OnFlyStarted;
        controls.Player.Fly.canceled -= OnFlyCanceled;

        controls.Player.QuickBoost.performed -= OnQuickBoost;

        controls.Player.Disable();
    }

    private void FixedUpdate()
    {
        if (isQuickBoosting)
        {
            DoQuickBoostStep();
            return; // skip normal movement during dash
        }

        float leftRightMoveSpeed = boostHeld ? boostSpeed : walkSpeed;
        rb.linearVelocity = new Vector2(moveInputDirection * leftRightMoveSpeed, rb.linearVelocity.y);

        bool grounded = IsGrounded();
        if (flyHeld)
        {
            rb.gravityScale = flyGravityScale;

            rb.AddForce(Vector2.up * flyAcceleration, ForceMode2D.Force);

            // cap upward speed
            if (rb.linearVelocity.y > maxFlyUpSpeed)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, maxFlyUpSpeed);
        }
        else
        {
            rb.gravityScale = normalGravityScale;

            //clear fly state when landing
            if (grounded) flyHeld = false;
        }

        ClampFallSpeed();
    }

    private void ClampFallSpeed()
    {
        if (rb.linearVelocity.y < -maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
    }

    private void OnJump(InputAction.CallbackContext ctx)
    {
        if (IsGrounded())
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    private void OnQuickBoost(InputAction.CallbackContext ctx)
    {
        if (isQuickBoosting) return;
        if (quickBoostCooldownTimer > 0f) return;

        // Determine dash direction:
        // - If player is holding left/right, dash that way
        // - Otherwise dash in last facing direction
        int direction = 0;
        if (moveInputDirection > 0.2f) direction = 1;
        else if (moveInputDirection < -0.2f) direction = -1;
        else direction = facingDirection;

        // Safety: if somehow facingDirection is 0, default to right
        if (direction == 0) direction = 1;

        // Save current vertical state so we can restore it after dash if desired
        savedGravityScale = rb.gravityScale;
        savedYVelocity = rb.linearVelocity.y;

        quickBoostDir = direction;
        quickBoostTimer = 0f;
        isQuickBoosting = true;
        quickBoostCooldownTimer = quickBoostCooldown;

        // Track if we were flying before the quick boost to resume upward momentum later
        bool grounded = IsGrounded();
        wasFlyingBeforeQuickBoost = (!grounded && flyHeld);

        quickBoostStartSpeed = quickBoostSpeed;

        // Crisp dash: wipe horizontal velocity first
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // Immediate burst
        rb.linearVelocity = new Vector2(quickBoostDir * quickBoostStartSpeed, rb.linearVelocity.y);
    }

    private bool IsGrounded()
    {
        Vector2 bottomCenterPoint = new Vector2(col.bounds.center.x, col.bounds.min.y);
        Vector2 size = new Vector2(col.bounds.size.x * 0.9f, 0.05f);
        float dist = groundCheckDistance;

        RaycastHit2D hit = Physics2D.BoxCast(bottomCenterPoint, size, 0f, Vector2.down, dist, groundLayer);
        return hit.collider != null;
    }

    private void DoQuickBoostStep()
    {
        quickBoostTimer += Time.fixedDeltaTime;

        // Save current vertical state so we can restore it after dash if desired
        savedGravityScale = rb.gravityScale;
        savedYVelocity = rb.linearVelocity.y;

        // DURING dash: zero out vertical movement and gravity
        rb.gravityScale = 0f;

        // Lock vertical movement during dash (horizontal line)
        rb.linearVelocity = new Vector2(quickBoostDir * quickBoostSpeed, 0f);

        float timeRemainingPercent = Mathf.Clamp01(quickBoostTimer / quickBoostDuration);
        float multiplier = quickBoostCurve.Evaluate(timeRemainingPercent);

        float targetVelocity = quickBoostDir * quickBoostStartSpeed * multiplier;
        rb.linearVelocity = new Vector2(targetVelocity, rb.linearVelocity.y);

        if (timeRemainingPercent >= 1f)
        {
            // AFTER dash: if we were flying, restore upward momentum
            if (wasFlyingBeforeQuickBoost || flyHeld)
            {
                // keep fly held so thrust continues if Space is still held
                // (if you want it only if still holding Fly, gate with: if (flyHeld) ...)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, quickBoostFlyExitUpVelocity);
            }

            wasFlyingBeforeQuickBoost = false;
            isQuickBoosting = false;
        }
    }

    private void OnFlyStarted(InputAction.CallbackContext ctx) => flyHeld = true;
    private void OnFlyCanceled(InputAction.CallbackContext ctx) => flyHeld = false;

    private void OnBoostStarted(InputAction.CallbackContext ctx) => boostHeld = true;
    private void OnBoostCanceled(InputAction.CallbackContext ctx) => boostHeld = false;
}
