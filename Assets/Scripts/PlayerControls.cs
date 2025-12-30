using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerControls : MonoBehaviour
{
    // Cached components
    private Rigidbody2D rb;
    private Actions controls;
    private Collider2D col;

    // Ground probing
    private ContactFilter2D groundFilter;
    private float groundProbeOffset = 0.01f;
    private Vector2 groundBoxOffset;

    // Horizontal input (-1..1)
    private float moveInputDirection;

    #region Acceleration settings
    [Header("Acceleration")]
    [SerializeField] private float groundAccel = 60f;      // ground acceleration when moving
    [SerializeField] private float groundDecel = 80f;      // ground deceleration when no input
    [SerializeField] private float groundTurnAccel = 110f; // ground reverse accel
    [SerializeField] private float airAccel = 40f;         // air control acceleration
    [SerializeField] private float airDecel = 15f;         // air drag when no input
    [SerializeField] private float airTurnAccel = 80f;     // air reverse accel
    #endregion

    #region Move settings
    [Header("Move")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float boostSpeed = 9f;
    public const float MoveDeadzone = 0.2f;
    #endregion

    #region Jump settings
    [Header("Jump")]
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private LayerMask groundLayer;
    private bool jumpedFromGround; // gates flight until apex after a ground jump
    private float coyoteTime = 0.1f; // allow jump shortly after leaving ground
    private float timeSinceLastGrounded;
    #endregion

    #region Jump assist
    [Header("Jump Assist")]
    [SerializeField] private float jumpBufferTime = 0.12f; // buffer input before you land
    private float jumpBufferTimer;
    #endregion

    #region Boost / Fly settings
    [Header("Boost")]
    private bool boostHeld;

    [Header("Fly")]
    [SerializeField] private float flyAcceleration = 30f;   // upward force while holding fly
    [SerializeField] private float maxFlyUpSpeed = 4.5f;    // upward speed cap while flying
    [SerializeField] private float flyGravityScale = 2f;    // gravity while flying
    [SerializeField] private float normalGravityScale = 3f; // gravity when not flying
    #endregion

    #region Quick boost (dash) settings
    [Header("Quick Boost")]
    [SerializeField] private float quickBoostStartSpeed = 16f;       // configured dash speed
    [SerializeField] private float quickBoostDuration = 0.35f;       // dash duration
    [SerializeField] private AnimationCurve quickBoostCurve = null;  // speed-over-time curve (1->0)
    [SerializeField] private float quickBoostCooldown = 0.4f;

    // Quick Boost state
    private bool isQuickBoosting;
    private float quickBoostTimer;
    private float quickBoostCooldownTimer;
    private int quickBoostDir; // -1 or +1

    // Quick Boost exit tuning
    [SerializeField] private float quickBoostFlyExitUpVelocity = 10f;
    [SerializeField] private float quickBoostNeutralExitSpeed = 2f;

    [Header("Quick Boost Acceleration")]
    [SerializeField] private float quickBoostAccel = 200f;
    [SerializeField] private float quickBoostDecel = 260f;
    [SerializeField, Range(0f, 1f)] private float quickBoostMinMultiplier = 0.18f; // prevents near-zero tail
    [SerializeField] private bool wipeHorizontalOnQuickBoostStart = true;
    #endregion

    #region QB -> Fly carry / chaining
    [Header("QB -> Fly Carry")]
    [SerializeField] private float qbFlyCarryTime = 0.18f;     // protect QB horizontal after exit
    [SerializeField, Range(0f, 1f)] private float qbFlyReleasePercent = 0.85f; // early QB->fly release
    private float qbFlyCarryTimer;
    private float qbCarryVx;

    [Header("QB Chaining")]
    [SerializeField] private float qbChainBufferTime = 0.2f;
    [SerializeField, Range(0f, 1f)] private float qbChainStartPercent = 0.8f;
    [SerializeField] private float qbChainMinInterval = 0.05f;

    private float qbChainBufferTimer;
    private bool qbChainQueued;
    private int qbQueuedDir;
    private float qbChainIntervalTimer;
    #endregion

    [Header("Fall")]
    [SerializeField] private float maxFallSpeed = 7f;

    // Facing / state
    private int facingDirection = 1;
    private bool wasFlyingBeforeQuickBoost;

    // Input tracking
    private bool flyKeyHeld;
    private bool jumpKeyHeld;
    private bool anyFlyInputHeld => jumpKeyHeld || flyKeyHeld;

    // Reusable overlap buffer to avoid allocations
    private readonly Collider2D[] m_overlapResults = new Collider2D[1];

    // ------------------------
    // Unity lifecycle methods
    // ------------------------

    // Read non-physics inputs and update frame-based cooldowns.
    void Update()
    {
        // Read horizontal axis through Input System (guarded if controls not created).
        moveInputDirection = controls != null ? controls.Player.Walk.ReadValue<float>() : 0f;

        // Update visual/logic facing based on input
        int dir = AxisToDir(moveInputDirection);
        if (dir != 0) facingDirection = dir;

        // Quick-boost cooldown reduced per-frame (smoother feel than fixedstep).
        if (quickBoostCooldownTimer > 0f)
        {
            quickBoostCooldownTimer -= Time.deltaTime;
            if (quickBoostCooldownTimer < 0f) quickBoostCooldownTimer = 0f;
        }
    }

    // Cache components and defaults.
    void Awake()
    {
        // Initialize input system
        controls = new Actions();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        // Find ground layer
        if (groundLayer == 0)
            groundLayer = LayerMask.GetMask("Ground");

        groundFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = groundLayer,
            useTriggers = false
        };

        groundBoxOffset = Vector2.down * groundProbeOffset;
        rb.gravityScale = normalGravityScale;

        // Ensure a safe default curve so Evaluate never NREs.
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
        // Dispose generated input actions to free native resources.
        if (controls != null)
        {
            controls.Dispose();
            controls = null;
        }

        // If destroyed mid-dash, restore gravity.
        if (rb != null)
            rb.gravityScale = normalGravityScale;
    }

    private void OnEnable()
    {
        if (controls == null) return;

        controls.Player.Enable();

        // Subscribe input callbacks (Jump / Fly / Boost / QuickBoost)
        controls.Player.Jump.started += OnJumpStarted;
        controls.Player.Jump.canceled += OnJumpCanceled;
        
        controls.Player.GroundBoost.started += OnBoostStarted;
        controls.Player.GroundBoost.canceled += OnBoostCanceled;

        controls.Player.Fly.started += OnFlyStarted;
        controls.Player.Fly.canceled += OnFlyCanceled;

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

        // Safety: restore physics state if we get disabled mid-dash.
        if (rb != null)
        {
            rb.gravityScale = normalGravityScale;
            isQuickBoosting = false;
        }
    }

    // Physics loop: timers, ground state, movement, flight, clamps.
    private void FixedUpdate()
    {
        TickFixedTimers();

        bool groundedNow = UpdateGroundState();

        if (isQuickBoosting)
        {
            DoQuickBoostStep();
            return; // skip normal movement while dashing
        }

        ProcessHorizontalMovement(groundedNow);

        ProcessFlight(groundedNow);

        ClampFallSpeed();
    }

    // ------------------------
    // Helper / small methods
    // ------------------------

    // Centralized fixed-timestep timer updates to avoid duplication.
    private void TickFixedTimers()
    {
        float dt = Time.fixedDeltaTime;
        if (jumpBufferTimer > 0f) jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);
        if (qbChainBufferTimer > 0f) qbChainBufferTimer = Mathf.Max(0f, qbChainBufferTimer - dt);
        if (qbChainIntervalTimer > 0f) qbChainIntervalTimer = Mathf.Max(0f, qbChainIntervalTimer - dt);
        if (qbFlyCarryTimer > 0f) qbFlyCarryTimer = Mathf.Max(0f, qbFlyCarryTimer - dt);
    }

    // Update grounded timers and return current grounded state.
    private bool UpdateGroundState()
    {
        bool groundedNow = IsGrounded();
        if (groundedNow) timeSinceLastGrounded = 0f;
        else timeSinceLastGrounded += Time.fixedDeltaTime;
        return groundedNow;
    }

    // Horizontal movement separated for clarity and testing.
    private void ProcessHorizontalMovement(bool groundedNow)
    {
        float maxSpeed = CurrentHorizontalMoveSpeed();
        float targetVelocity = moveInputDirection * maxSpeed;
        float currentVelocity = rb.linearVelocity.x;
        float dt = Time.fixedDeltaTime;
        int carryDir = AxisToDir(qbCarryVx);

        // Protect QB carry horizontal speed while carry timer active.
        bool protectCarry = qbFlyCarryTimer > 0f;
        if (protectCarry)
        {
            int heldDirForCarry = AxisToDir(moveInputDirection);
            if (heldDirForCarry == 0 || heldDirForCarry == carryDir)
            {
                if (carryDir > 0) targetVelocity = Mathf.Max(targetVelocity, qbCarryVx);
                else if (carryDir < 0) targetVelocity = Mathf.Min(targetVelocity, qbCarryVx);
            }
        }

        if (groundedNow)
        {
            // Grounded: deterministic MoveTowards-style acceleration & deceleration.
            bool hasInput = Mathf.Abs(moveInputDirection) > 0.001f;
            bool reversing = hasInput &&
                             Mathf.Sign(targetVelocity) != Mathf.Sign(currentVelocity) &&
                             Mathf.Abs(currentVelocity) > 0.1f;

            float accelRate;
            if (!hasInput) accelRate = groundDecel;
            else if (reversing) accelRate = groundTurnAccel;
            else accelRate = groundAccel;

            float newVx = Mathf.MoveTowards(currentVelocity, targetVelocity, accelRate * dt);
            rb.linearVelocity = new Vector2(newVx, rb.linearVelocity.y);
        }
        else
        {
            // Air: apply thrust while input, otherwise apply drag.
            bool hasInput = Mathf.Abs(moveInputDirection) > 0.001f;

            if (hasInput)
            {
                // Thrust amount. Use airTurnAccel when reversing, otherwise airAccel.
                bool reversing = Mathf.Sign(moveInputDirection) != Mathf.Sign(currentVelocity) &&
                                 Mathf.Abs(currentVelocity) > 0.1f;

                float thrust = reversing ? airTurnAccel : airAccel;
                // Apply horizontal thrust (ForceMode2D.Force acts like "acceleration" for a given mass).
                rb.AddForce(Vector2.right * (moveInputDirection * thrust), ForceMode2D.Force);

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
                float newVx = Mathf.MoveTowards(vx, 0f, airDecel * dt);

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

    // Flight logic separated from movement for easier tuning.
    private void ProcessFlight(bool groundedNow)
    {
        // If we jumped from ground and still rising, block flight until apex.
        bool isInJumpRisePhase = jumpedFromGround && rb.linearVelocity.y > 0f;
        bool allowFlightNow = !isInJumpRisePhase;

        bool shouldFlyNow = allowFlightNow && (flyKeyHeld || jumpKeyHeld);

        if (shouldFlyNow)
        {
            TryFly();
            jumpedFromGround = false; // once flying, clear the gate
        }
        else
        {
            rb.gravityScale = normalGravityScale;
        }
    }

    // Convert axis to -1/0/1 using the configured deadzone.
    private static int AxisToDir(float axis)
    {
        if (axis > MoveDeadzone) return 1;
        if (axis < -MoveDeadzone) return -1;
        return 0;
    }

    // Apply flying forces & gravity changes.
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

    // Prevent infinite falling velocity.
    private void ClampFallSpeed()
    {
        if (rb.linearVelocity.y < -maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
    }

    // ------------------------
    // Jump callbacks & helpers
    // ------------------------

    private void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        jumpKeyHeld = true;
        jumpBufferTimer = jumpBufferTime;

        bool canJump = timeSinceLastGrounded <= coyoteTime && jumpBufferTimer > 0f;
        if (canJump)
        {
            PerformJump();
            // consume buffer + prevent immediate re-jump
            jumpBufferTimer = 0f;
            timeSinceLastGrounded = coyoteTime + 1f;
        }
    }

    // Execute the jump impulse and mark state to gate flight until apex.
    private void PerformJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        jumpedFromGround = true;
    }

    private void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        jumpKeyHeld = false;
        jumpedFromGround = false;
    }

    // ------------------------
    // Quick Boost (dash) input & stepper
    // ------------------------

    private void OnQuickBoost(InputAction.CallbackContext ctx)
    {
        // Queue a chain if already QBing.
        if (isQuickBoosting)
        {
            // small interval prevents double-queue from one input event or weird repeats
            if (qbChainIntervalTimer > 0f) return;

            int direction = AxisToDir(moveInputDirection);
            if (direction == 0) direction = facingDirection != 0 ? facingDirection : 1;

            qbChainQueued = true;
            qbQueuedDir = direction;
            qbChainBufferTimer = qbChainBufferTime;
            qbChainIntervalTimer = qbChainMinInterval;

            return;
        }

        if (quickBoostCooldownTimer > 0f) return;

        int directionStart = AxisToDir(moveInputDirection);
        // Safety: if somehow facingDirection is 0, default to right
        if (directionStart == 0) directionStart = facingDirection != 0 ? facingDirection : 1;

        quickBoostDir = directionStart;
        quickBoostTimer = 0f;
        isQuickBoosting = true;
        quickBoostCooldownTimer = quickBoostCooldown;

        bool grounded = IsGrounded();
        wasFlyingBeforeQuickBoost = (!grounded && anyFlyInputHeld);

        rb.gravityScale = 0f;

        // Optionally wipe horizontal velocity for crisp dash start and lock vertical.
        rb.linearVelocity = new Vector2(
            wipeHorizontalOnQuickBoostStart ? 0f : rb.linearVelocity.x,
            0f
        );

        qbChainQueued = false;
        qbChainBufferTimer = 0f;
        qbChainIntervalTimer = qbChainMinInterval;
    }

    // Ground probe: OverlapBox with a small downward offset to avoid false positives.
    private bool IsGrounded()
    {
        Vector2 bottomCenterPoint =
            (Vector2)col.bounds.center + Vector2.down * (col.bounds.extents.y) + groundBoxOffset;

        Vector2 groundBoxSize = new Vector2(col.bounds.size.x * 0.9f, 0.08f);

        return Physics2D.OverlapBox(bottomCenterPoint, groundBoxSize, 0f, groundFilter, m_overlapResults) > 0;
    }

    private float CurrentHorizontalMoveSpeed()
    {
        return boostHeld ? boostSpeed : walkSpeed;
    }

    // Top-level QB driver.
    private void DoQuickBoostStep()
    {
        // chaining -> apply velocity -> check end
        if (HandleQBChaining()) return;
        ApplyQBVelocity();
        CheckQBEnd();
    }

    // If a chain request exists and timing meets threshold, start the new QB immediately.
    private bool HandleQBChaining()
    {
        float timeRemainingPercentage = Mathf.Clamp01(quickBoostTimer / quickBoostDuration);
        if (qbChainQueued && qbChainBufferTimer > 0f && timeRemainingPercentage >= qbChainStartPercent)
        {
            quickBoostDir = qbQueuedDir;
            quickBoostTimer = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

            qbChainQueued = false;
            qbChainBufferTimer = 0f;
            qbChainIntervalTimer = qbChainMinInterval;

            return true;
        }

        return false;
    }

    // Apply per-frame QB velocity using curve + accel/decel rules.
    private void ApplyQBVelocity()
    {
        quickBoostTimer += Time.fixedDeltaTime;

        int heldDir = AxisToDir(moveInputDirection);
        rb.gravityScale = 0f;
        float timeRemainingPercentage = Mathf.Clamp01(quickBoostTimer / quickBoostDuration);

        if (quickBoostCurve == null)
        {
            quickBoostCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 0.35f), new Keyframe(1f, 0f));
        }

        float curveMultiplier = quickBoostCurve.Evaluate(timeRemainingPercentage);
        curveMultiplier = Mathf.Max(curveMultiplier, quickBoostMinMultiplier);

        float targetSpeedAbs = quickBoostStartSpeed * curveMultiplier;

        if (heldDir != 0 && heldDir == quickBoostDir)
            targetSpeedAbs = Mathf.Max(targetSpeedAbs, CurrentHorizontalMoveSpeed());

        float targetVelocity = quickBoostDir * targetSpeedAbs;
        float currentVelocity = rb.linearVelocity.x;

        float rate;
        if (heldDir != 0 && heldDir == quickBoostDir)
            rate = quickBoostAccel;
        else
            rate = (Mathf.Abs(targetVelocity) > Mathf.Abs(currentVelocity)) ? quickBoostAccel : quickBoostDecel;

        float newVx = Mathf.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newVx, 0f);
    }

    // Check for QB->fly release or QB end and handle exit.
    private void CheckQBEnd()
    {
        float timeRemainingPercentage = Mathf.Clamp01(quickBoostTimer / quickBoostDuration);
        bool wantsFly = anyFlyInputHeld && !IsGrounded();

        if (wantsFly && timeRemainingPercentage >= qbFlyReleasePercent)
        {
            EndQuickBoostIntoCarry(wantsFly: true);
            return;
        }

        if (timeRemainingPercentage >= 1f)
            EndQuickBoostIntoCarry(wantsFly);
    }

    // Exit QB: capture carry, set gravity, and apply exit velocities.
    private void EndQuickBoostIntoCarry(bool wantsFly)
    {
        qbCarryVx = rb.linearVelocity.x;
        qbFlyCarryTimer = qbFlyCarryTime;

        rb.gravityScale = wantsFly ? flyGravityScale : normalGravityScale;

        int heldDir = AxisToDir(moveInputDirection);
        float exitVx;

        if (heldDir != 0 && heldDir == quickBoostDir)
            exitVx = heldDir * CurrentHorizontalMoveSpeed();
        else
            exitVx = quickBoostDir * quickBoostNeutralExitSpeed;

        if (Mathf.Abs(qbCarryVx) > Mathf.Abs(exitVx))
            exitVx = qbCarryVx;

        float exitVy = 0f;
        if (wantsFly && wasFlyingBeforeQuickBoost)
            exitVy = quickBoostFlyExitUpVelocity;

        rb.linearVelocity = new Vector2(exitVx, exitVy);

        wasFlyingBeforeQuickBoost = false;
        isQuickBoosting = false;
    }

    // ------------------------
    // Input callbacks
    // ------------------------
    private void OnFlyStarted(InputAction.CallbackContext ctx) => flyKeyHeld = true;
    private void OnFlyCanceled(InputAction.CallbackContext ctx) => flyKeyHeld = false;

    private void OnBoostStarted(InputAction.CallbackContext ctx) => boostHeld = true;
    private void OnBoostCanceled(InputAction.CallbackContext ctx) => boostHeld = false;
}
