using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerControls : MonoBehaviour
{
    private Rigidbody2D rb;
    private Actions controls;
    private Collider2D col;
    private ContactFilter2D groundFilter;
    private float groundProbeOffset = 0.01f;
    private Vector2 groundBoxOffset;
    private float moveInputDirection;

    [Header("Move")]
    public float walkSpeed = 5f;
    public float boostSpeed = 9f;
    public const float MoveDeadzone = 0.2f;

    [Header("Jump")]
    public float jumpForce = 10f;
    public LayerMask groundLayer;
    private bool jumpedFromGround; // used to delay W-fly until apex

    [Header("Boost")]
    private bool boostHeld;

    [Header("Fly")]
    public float flyAcceleration = 30f;      // upward acceleration while holding Space
    public float maxFlyUpSpeed = 4.5f;         // cap upward speed
    public float flyGravityScale = 2f;     // gravity while flying
    public float normalGravityScale = 3f;    // gravity normally

    [Header("Quick Boost")]
    public float quickBoostStartSpeed = 16f;     // initial burst speed
    public float quickBoostDuration = 0.35f; // seconds
    public AnimationCurve quickBoostCurve = null; // optional; if null we use a built-in ease
    public float quickBoostCooldown = 0.4f;

    // Quick Boost state
    private bool isQuickBoosting;
    private float quickBoostTimer;
    private float quickBoostCooldownTimer;
    private int quickBoostDir;              // -1 or +1

    // Quick Boost exit tuning
    public float quickBoostFlyExitUpVelocity = 10f; // tune: how much upward momentum to resume with
    public float quickBoostNeutralExitSpeed = 2f;      // tune: horizontal speed when exiting dash with no input

    [Header("Fall")]
    public float maxFallSpeed = 7f;

    // Facing direction: -1 = left, +1 = right
    private int facingDirection = 1;
    private bool wasFlyingBeforeQuickBoost;

    // Input tracking
    private bool flyKeyHeld;
    private bool jumpKeyHeld;
    private bool anyFlyInputHeld => jumpKeyHeld || flyKeyHeld;

    // Reusable buffer to avoid allocations when checking ground contacts
    private readonly Collider2D[] m_overlapResults = new Collider2D[1];

    void Start()
    {

    }

    void Update()
    {
        // Left-Right movement input with guard controls
        moveInputDirection = controls != null ? controls.Player.Walk.ReadValue<float>() : 0f;

        // Update facing direction
        int dir = AxisToDir(moveInputDirection);
        if (dir != 0) facingDirection = dir;

        // Quick Boost Cooldown
        if (quickBoostCooldownTimer > 0f)
        {
            quickBoostCooldownTimer -= Time.deltaTime;
            if (quickBoostCooldownTimer < 0f) quickBoostCooldownTimer = 0f;
        }
    }

    void Awake()
    {
        // Initialize input system
        controls = new Actions();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        // Find ground layer
        if (groundLayer == 0)
        {
            groundLayer = LayerMask.GetMask("Ground");
        }

        groundFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = groundLayer,
            useTriggers = false
        };

        groundBoxOffset = Vector2.down * groundProbeOffset;

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
    }

    private void OnDestroy()
    {
        // Dispose the generated action set to free native resources
        if (controls != null)
        {
            controls.Dispose();
            controls = null;
        }

        // Also ensure physics restored if object destroyed mid-dash
        if (rb != null)
            rb.gravityScale = normalGravityScale;
    }

    private void OnEnable()
    {
        if (controls == null) return;

        controls.Player.Enable();

        // Jump
        controls.Player.Jump.started += OnJumpStarted;
        controls.Player.Jump.canceled += OnJumpCanceled;

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
        if (controls != null)
        {
            controls.Player.Jump.started -= OnJumpStarted;
            controls.Player.Jump.canceled -= OnJumpCanceled;

            controls.Player.GroundBoost.started -= OnBoostStarted;
            controls.Player.GroundBoost.canceled -= OnBoostCanceled;

            controls.Player.Fly.started -= OnFlyStarted;
            controls.Player.Fly.canceled -= OnFlyCanceled;

            controls.Player.QuickBoost.performed -= OnQuickBoost;

            controls.Player.Disable();
        }

        // Safety: ensure physics state is sane if the component/script is disabled during a dash.
        if (rb != null)
        {
            rb.gravityScale = normalGravityScale;
            isQuickBoosting = false;
        }
    }

    private void FixedUpdate()
    {
        if (isQuickBoosting)
        {
            DoQuickBoostStep();
            return; // skip normal movement during dash
        }

        // Basic left-right movement
        float leftRightMoveSpeed = CurrentHorizontalMoveSpeed();
        rb.linearVelocity = new Vector2(moveInputDirection * leftRightMoveSpeed, rb.linearVelocity.y);

        bool isInJumpRisePhase = jumpedFromGround && rb.linearVelocity.y > 0f;

        // block flight while still rising from a ground-initiated jump
        bool allowFlightNow = !isInJumpRisePhase;

        bool shouldFlyNow = allowFlightNow && (flyKeyHeld || jumpKeyHeld);

        if (shouldFlyNow)
        {
            // Start flight immediately (even if grounded) unless we're in the rise phase after a ground jump.
            // TryFly handles gravity and upward acceleration; calling it on-ground gives takeoff thrust.
            TryFly();

            // Once flight begins, clear the jumped-from-ground flag so apex gating won't reapply.
            jumpedFromGround = false;
        } else
        {
            // Apply normal gravity if not flying
            rb.gravityScale = normalGravityScale;
        }

            ClampFallSpeed();
    }

    private static int AxisToDir(float axis)
    {
        if (axis > MoveDeadzone) return 1;
        if (axis < -MoveDeadzone) return -1;
        return 0;
    }

    private void TryFly()
    {
        rb.gravityScale = flyGravityScale;

        // prevent downward velocity while flying
        if (rb.linearVelocity.y < 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);


        // apply upward acceleration
        rb.AddForce(Vector2.up * flyAcceleration, ForceMode2D.Force);

        // cap upward speed
        if (rb.linearVelocity.y > maxFlyUpSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, maxFlyUpSpeed);
    }

    private void ClampFallSpeed()
    {
        if (rb.linearVelocity.y < -maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
    }

    private void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        jumpKeyHeld = true;

        if (IsGrounded())
        {
            PerformJump();
        }
    }

    private void PerformJump()
    {
        // Apply jump force
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        // Mark that we jumped from ground to gate flight until apex
        jumpedFromGround = true;
    }

    private void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        jumpKeyHeld = false;

        // If they let go of Jump, stop the “apex-to-fly” transition
        jumpedFromGround = false;
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

        quickBoostDir = direction;
        quickBoostTimer = 0f;
        isQuickBoosting = true;
        quickBoostCooldownTimer = quickBoostCooldown;

        // Track if we were flying before the quick boost to resume upward momentum later
        bool grounded = IsGrounded();
        wasFlyingBeforeQuickBoost = (!grounded && anyFlyInputHeld);

        // Crisp dash: wipe horizontal velocity first
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // Immediate burst, good for frame-zero responsiveness
        rb.linearVelocity = new Vector2(quickBoostDir * quickBoostStartSpeed, rb.linearVelocity.y);
    }

    private bool IsGrounded()
    {
        Vector2 bottomCenterPoint = (Vector2)col.bounds.center + Vector2.down * (col.bounds.extents.y) + groundBoxOffset;
        Vector2 groundBoxSize = new Vector2(col.bounds.size.x * 0.9f, 0.08f);

        return Physics2D.OverlapBox(bottomCenterPoint, groundBoxSize, 0f, groundFilter, m_overlapResults) > 0;
    }

    private float CurrentHorizontalMoveSpeed()
    {
        return boostHeld ? boostSpeed : walkSpeed;
    }

    private void DoQuickBoostStep()
    {
        quickBoostTimer += Time.fixedDeltaTime;

        // Determine held direction
        int heldDir = AxisToDir(moveInputDirection);

        rb.gravityScale = 0f; // no gravity during dash
        float timeRemainingPercent = Mathf.Clamp01(quickBoostTimer / quickBoostDuration);

        // Prevent the tail from slowing to a near-stop before exit speed kicks in
        float qbCurveMultiplier = quickBoostCurve.Evaluate(timeRemainingPercent);

        float qbSpeedAtThisFrame = quickBoostStartSpeed * qbCurveMultiplier;
        // Don't go below horizontal movespeed if still pressing towards dash direction
        if (heldDir != 0 && heldDir == quickBoostDir)
        {
            qbSpeedAtThisFrame = Mathf.Max(qbSpeedAtThisFrame, CurrentHorizontalMoveSpeed());
        }

        // Set horizontal dash speed + lock vertical movement during dash
        rb.linearVelocity = new Vector2(quickBoostDir * qbSpeedAtThisFrame, 0f);

        // If dash ends
        if (timeRemainingPercent >= 1f)
        {
            // Restore gravity immediately when dash ends
            rb.gravityScale = normalGravityScale;

            // Determine exit horizontal velocities
            float qbExitVelocity;
            if (heldDir != 0 && heldDir == quickBoostDir)
            {
                // If they are holding a direction, exit at max move speed in that direction
                qbExitVelocity = heldDir * CurrentHorizontalMoveSpeed();
            }
            else
            {
                // No input: small drift
                qbExitVelocity = quickBoostDir * quickBoostNeutralExitSpeed;
            }

            // Determine exit vertical velocity
            float exitVy = 0f;
            if (wasFlyingBeforeQuickBoost && anyFlyInputHeld)
                exitVy = quickBoostFlyExitUpVelocity;

            // Apply exit velocities
            rb.linearVelocity = new Vector2(qbExitVelocity, exitVy);

            wasFlyingBeforeQuickBoost = false;
            isQuickBoosting = false;
        }
    }

    private void OnFlyStarted(InputAction.CallbackContext ctx) => flyKeyHeld = true;
    private void OnFlyCanceled(InputAction.CallbackContext ctx) => flyKeyHeld = false;

    private void OnBoostStarted(InputAction.CallbackContext ctx) => boostHeld = true;
    private void OnBoostCanceled(InputAction.CallbackContext ctx) => boostHeld = false;
}
