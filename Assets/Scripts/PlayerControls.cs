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

    [Header("Acceleration")]
    [SerializeField] private float groundAccel = 60f;      // how fast you ramp up on ground
    [SerializeField] private float groundDecel = 80f;      // how fast you stop on ground
    [SerializeField] private float groundTurnAccel = 110f; // how fast you reverse direction on ground
    [SerializeField] private float airAccel = 40f;         // air control accel
    [SerializeField] private float airDecel = 15f;         // air drift stopping
    [SerializeField] private float airTurnAccel = 80f;     // air reverse accel

    [Header("Move")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float boostSpeed = 9f;
    public const float MoveDeadzone = 0.2f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private LayerMask groundLayer;
    private bool jumpedFromGround; // used to delay W-fly until apex
    private float coyoteTime = 0.1f;
    private float timeSinceLastGrounded;

    [Header("Jump Assist")]
    [SerializeField] private float jumpBufferTime = 0.12f; // 80–150ms feels good
    private float jumpBufferTimer;

    [Header("Boost")]
    private bool boostHeld;

    [Header("Fly")]
    [SerializeField] private float flyAcceleration = 30f;   // upward acceleration while holding Space
    [SerializeField] private float maxFlyUpSpeed = 4.5f;    // cap upward speed
    [SerializeField] private float flyGravityScale = 2f;    // gravity while flying
    [SerializeField] private float normalGravityScale = 3f; // gravity normally

    [Header("Quick Boost")]
    [SerializeField] private float quickBoostStartSpeed = 16f;       // initial burst speed
    [SerializeField] private float quickBoostDuration = 0.35f;       // seconds
    [SerializeField] private AnimationCurve quickBoostCurve = null;  // optional; if null we use a built-in ease
    [SerializeField] private float quickBoostCooldown = 0.4f;

    // Quick Boost state
    private bool isQuickBoosting;
    private float quickBoostTimer;
    private float quickBoostCooldownTimer;
    private int quickBoostDir; // -1 or +1

    // Quick Boost exit tuning
    [SerializeField] private float quickBoostFlyExitUpVelocity = 10f; // tune: how much upward momentum to resume with
    [SerializeField] private float quickBoostNeutralExitSpeed = 2f;   // tune: horizontal speed when exiting dash with no input

    [Header("Quick Boost Acceleration")]
    [SerializeField] private float quickBoostAccel = 200f;     // ramps up toward target speed
    [SerializeField] private float quickBoostDecel = 260f;     // ramps down as curve tails off
    [SerializeField, Range(0f, 1f)] private float quickBoostMinMultiplier = 0.18f; // prevents "near stop" tail
    [SerializeField] private bool wipeHorizontalOnQuickBoostStart = true;

    [Header("QB -> Fly Carry")]
    [SerializeField] private float qbFlyCarryTime = 0.18f;     // how long to protect QB momentum after QB ends
    [SerializeField, Range(0f, 1f)] private float qbFlyReleasePercent = 0.85f; // allow early release into fly near end
    private float qbFlyCarryTimer;
    private float qbCarryVx;

    [Header("QB Chaining")]
    [SerializeField] private float qbChainBufferTime = 0.2f;          // how long a QB press is remembered while QB is active
    [SerializeField, Range(0f, 1f)] private float qbChainStartPercent = 0.8f; // earliest percent you can chain into the next QB
    [SerializeField] private float qbChainMinInterval = 0.05f;        // tiny anti-spam / prevents multiple chains in same frame

    private float qbChainBufferTimer;
    private bool qbChainQueued;
    private int qbQueuedDir;
    private float qbChainIntervalTimer;

    [Header("Fall")]
    [SerializeField] private float maxFallSpeed = 7f;

    // Facing direction: -1 = left, +1 = right
    private int facingDirection = 1;
    private bool wasFlyingBeforeQuickBoost;

    // Input tracking
    private bool flyKeyHeld;
    private bool jumpKeyHeld;
    private bool anyFlyInputHeld => jumpKeyHeld || flyKeyHeld;

    // Reusable buffer to avoid allocations when checking ground contacts
    private readonly Collider2D[] m_overlapResults = new Collider2D[1];

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
            groundLayer = LayerMask.GetMask("Ground");

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
        // consolidate timers
        TickFixedTimers();

        bool groundedNow = UpdateGroundState();

        if (isQuickBoosting)
        {
            DoQuickBoostStep();
            return; // skip normal movement during dash
        }

        ProcessHorizontalMovement(groundedNow);

        ProcessFlight(groundedNow);

        ClampFallSpeed();
    }

    // Centralized fixed-timestep timer tick to reduce duplicated code.
    private void TickFixedTimers()
    {
        float dt = Time.fixedDeltaTime;
        if (jumpBufferTimer > 0f) jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);
        if (qbChainBufferTimer > 0f) qbChainBufferTimer = Mathf.Max(0f, qbChainBufferTimer - dt);
        if (qbChainIntervalTimer > 0f) qbChainIntervalTimer = Mathf.Max(0f, qbChainIntervalTimer - dt);
        if (qbFlyCarryTimer > 0f) qbFlyCarryTimer = Mathf.Max(0f, qbFlyCarryTimer - dt);
    }

    private bool UpdateGroundState()
    {
        bool groundedNow = IsGrounded();
        if (groundedNow) timeSinceLastGrounded = 0f;
        else timeSinceLastGrounded += Time.fixedDeltaTime;
        return groundedNow;
    }

    private void ProcessHorizontalMovement(bool groundedNow)
    {
        // ---- accel-based horizontal ----
        float maxSpeed = CurrentHorizontalMoveSpeed();
        float targetVelocity = moveInputDirection * maxSpeed;
        float currentVelocity = rb.linearVelocity.x;
        float dt = Time.fixedDeltaTime;
        int carryDir = AxisToDir(qbCarryVx);

        // Carry protection
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
            // Air = "inertia": input applies thrust, and drag slows you when no input.
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

    private void ProcessFlight(bool groundedNow)
    {
        bool isInJumpRisePhase = jumpedFromGround && rb.linearVelocity.y > 0f;
        bool allowFlightNow = !isInJumpRisePhase;
        bool shouldFlyNow = allowFlightNow && (flyKeyHeld || jumpKeyHeld);

        if (shouldFlyNow)
        {
            TryFly();
            jumpedFromGround = false;
        }
        else
        {
            rb.gravityScale = normalGravityScale;
        }
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
        jumpBufferTimer = jumpBufferTime;

        bool canJump = timeSinceLastGrounded <= coyoteTime && jumpBufferTimer > 0f;
        if (canJump)
        {
            PerformJump();
            // consume buffer + prevent double jump until you leave ground again
            jumpBufferTimer = 0f;
            timeSinceLastGrounded = coyoteTime + 1f;
        }
    }

    private void PerformJump()
    {
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
        // If we are already QBing, queue a chain instead of ignoring the press.
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

        // Track if we were flying before the quick boost to resume upward momentum later
        bool grounded = IsGrounded();
        wasFlyingBeforeQuickBoost = (!grounded && anyFlyInputHeld);

        rb.gravityScale = 0f;

        // Wipe horizontal if desired, and lock vertical
        rb.linearVelocity = new Vector2(
            wipeHorizontalOnQuickBoostStart ? 0f : rb.linearVelocity.x,
            0f
        );

        // Clear any stale chain request
        qbChainQueued = false;
        qbChainBufferTimer = 0f;
        qbChainIntervalTimer = qbChainMinInterval;
    }

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

    private void DoQuickBoostStep()
    {
        // top-level splitter
        if (HandleQBChaining()) return;
        ApplyQBVelocity();
        CheckQBEnd();
    }

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

    private void ApplyQBVelocity()
    {
        quickBoostTimer += Time.fixedDeltaTime;

        int heldDir = AxisToDir(moveInputDirection);
        rb.gravityScale = 0f;   // no gravity during dash
        float timeRemainingPercentage = Mathf.Clamp01(quickBoostTimer / quickBoostDuration);

        // ensure curve
        if (quickBoostCurve == null)
        {
            quickBoostCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 0.35f), new Keyframe(1f, 0f));
        }

        float curveMultiplier = quickBoostCurve.Evaluate(timeRemainingPercentage);
        curveMultiplier = Mathf.Max(curveMultiplier, quickBoostMinMultiplier);

        float targetSpeedAbs = quickBoostStartSpeed * curveMultiplier;

        // If player holds dash direction, never drop below normal move speed during QB.
        if (heldDir != 0 && heldDir == quickBoostDir)
            targetSpeedAbs = Mathf.Max(targetSpeedAbs, CurrentHorizontalMoveSpeed());

        float targetVelocity = quickBoostDir * targetSpeedAbs;
        float currentVelocity = rb.linearVelocity.x;

        float rate;
        if (heldDir != 0 && heldDir == quickBoostDir)
        {
            rate = quickBoostAccel;
        }
        else
        {
            rate = (Mathf.Abs(targetVelocity) > Mathf.Abs(currentVelocity)) ? quickBoostAccel : quickBoostDecel;
        }

        float newVx = Mathf.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newVx, 0f);
    }

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
        {
            EndQuickBoostIntoCarry(wantsFly: wantsFly);
        }
    }

    private void EndQuickBoostIntoCarry(bool wantsFly)
    {
        // Capture QB horizontal for post-QB carry protection.
        qbCarryVx = rb.linearVelocity.x;
        qbFlyCarryTimer = qbFlyCarryTime;

        // Restore gravity
        rb.gravityScale = wantsFly ? flyGravityScale : normalGravityScale;

        // Horizontal exit:
        int heldDir = AxisToDir(moveInputDirection);
        float exitVx;

        if (heldDir != 0 && heldDir == quickBoostDir)
            exitVx = heldDir * CurrentHorizontalMoveSpeed();
        else
            exitVx = quickBoostDir * quickBoostNeutralExitSpeed;

        // Keep the stronger of carried QB speed vs the chosen exit speed (prevents a hitch).
        if (Mathf.Abs(qbCarryVx) > Mathf.Abs(exitVx))
            exitVx = qbCarryVx;

        float exitVy = 0f;

        // If we were flying before QB and still holding fly input, resume upward momentum.
        if (wantsFly && wasFlyingBeforeQuickBoost)
            exitVy = quickBoostFlyExitUpVelocity;

        rb.linearVelocity = new Vector2(exitVx, exitVy);

        wasFlyingBeforeQuickBoost = false;
        isQuickBoosting = false;
    }

    private void OnFlyStarted(InputAction.CallbackContext ctx) => flyKeyHeld = true;
    private void OnFlyCanceled(InputAction.CallbackContext ctx) => flyKeyHeld = false;

    private void OnBoostStarted(InputAction.CallbackContext ctx) => boostHeld = true;
    private void OnBoostCanceled(InputAction.CallbackContext ctx) => boostHeld = false;
}
