using MercMech.Common;
using UnityEngine;

public class QuickBoostMotor2D
{
    private readonly Rigidbody2D rb;

    // We need these to keep your existing behavior: speed floor uses move speed, QB exit picks fly gravity.
    private readonly Settings settings;
    private readonly HorizontalMotor2D.Settings moveSettings;
    private readonly FlightMotor2D.Settings flightSettings;

    [System.Serializable]
    public class Settings
    {
        [Header("Quick Boost")]

        [Tooltip("Configured initial dash speed (units/sec).\nSuggested range: 8 - 32")]
        public float quickBoostStartSpeed = 16f;

        [Tooltip("Dash duration in seconds.\nSuggested range: 0.18 - 0.6")]
        public float quickBoostDuration = 0.35f;

        [Tooltip("Speed-over-time curve (evaluated 0->1 across the dash). Leave null to use default ease.")]
        public AnimationCurve quickBoostCurve = null;

        [Tooltip("Cooldown between dashes in seconds.\nSuggested range: 0.1 - 1.0")]
        public float quickBoostCooldown = 0.4f;

        [Header("Quick Boost Exit Tuning")]

        [Tooltip("Upward velocity applied when exiting QB into flight if conditions meet.\nSuggested range: 4 - 14")]
        public float quickBoostFlyExitUpVelocity = 10f;

        [Tooltip("Neutral horizontal exit speed used when no input on dash exit.\nSuggested range: 0 - 4")]
        public float quickBoostNeutralExitSpeed = 2f;

        [Header("Quick Boost Acceleration")]

        [Tooltip("Ramps up toward the target QB speed. Higher = snappier accelerate.\nSuggested range: 50 - 400")]
        public float quickBoostAccel = 200f;

        [Tooltip("Ramps down during the tail. Higher = quicker tail decel.\nSuggested range: 100 - 600")]
        public float quickBoostDecel = 260f;

        [Tooltip("Prevents the QB tail from falling to near-zero. Multiply 0..1.")]
        [Range(0f, 1f)]
        public float quickBoostMinMultiplier = 0.18f; // prevents near-zero tail

        [Tooltip("If true, horizontal velocity is wiped at QB start for a crisp dash.")]
        public bool wipeHorizontalOnQuickBoostStart = true;

        [Header("QB -> Fly Carry")]

        [Tooltip("How long to protect QB horizontal momentum after QB ends (seconds).\nSuggested range: 0.05 - 0.4")]
        public float qbFlyCarryTime = 0.18f;

        [Tooltip("If QB -> Fly behavior is allowed early (percentage of dash completed).\n0..1")]
        [Range(0f, 1f)]
        public float qbFlyReleasePercent = 0.85f;

        [Header("QB Chaining")]

        [Tooltip("How long a QB press is remembered while QB is active (seconds).\nSuggested range: 0.05 - 0.4")]
        public float qbChainBufferTime = 0.2f;

        [Tooltip("Earliest percent of the dash where chaining into a new QB is allowed (0..1).")]
        [Range(0f, 1f)]
        public float qbChainStartPercent = 0.8f;

        [Tooltip("Tiny anti-spam interval between queue events (seconds).\nSuggested range: 0.01 - 0.1")]
        public float qbChainMinInterval = 0.05f;

        [Header("Quick Boost Exit -> Fly Multipliers")]

        [Tooltip("Multiplier applied to the upkick when QB->fly but you were NOT flying before QB. 0..1")]
        [Range(0f, 1f)]
        public float quickBoostFlyExitUpVelocityNonFlightMultiplier = 0.6f;

        [Tooltip("Multiplier applied to the upkick when QB->fly and you WERE flying before QB. 0..1")]
        [Range(0f, 1f)]
        public float quickBoostFlyExitUpVelocityFlightMultiplier = 1f;
    }

    // Quick Boost state (kept same names)
    public bool isQuickBoosting { get; private set; }

    // Added non-breaking PascalCase accessor for consistency with other motors.
    public bool IsQuickBoosting => isQuickBoosting;

    private float quickBoostTimer;
    private float quickBoostCooldownTimer;
    private int quickBoostDir; // -1 or +1

    // QB -> Fly carry / chaining (kept same names)
    public float qbFlyCarryTimer { get; private set; }
    public float qbCarryVx { get; private set; }

    private float qbChainBufferTimer;
    private bool qbChainQueued;
    private int qbQueuedDir;
    private float qbChainIntervalTimer;

    // Facing / state
    private bool wasFlyingBeforeQuickBoost;

    public QuickBoostMotor2D(Rigidbody2D rb, Settings settings, HorizontalMotor2D.Settings moveSettings, FlightMotor2D.Settings flightSettings)
    {
        this.rb = rb;
        this.settings = settings;
        this.moveSettings = moveSettings;
        this.flightSettings = flightSettings;
    }

    // Called from PlayerControls.Update (frame-based for smoother cooldown feel).
    public void TickQuickBoostCooldown(float dt)
    {
        if (quickBoostCooldownTimer > 0f)
        {
            quickBoostCooldownTimer -= dt;
            if (quickBoostCooldownTimer < 0f) quickBoostCooldownTimer = 0f;
        }
    }

    // Called from PlayerControls.FixedUpdate timer tick.
    public void TickFixedTimers(float dt)
    {
        if (qbChainBufferTimer > 0f) qbChainBufferTimer = Mathf.Max(0f, qbChainBufferTimer - dt);
        if (qbChainIntervalTimer > 0f) qbChainIntervalTimer = Mathf.Max(0f, qbChainIntervalTimer - dt);
        if (qbFlyCarryTimer > 0f) qbFlyCarryTimer = Mathf.Max(0f, qbFlyCarryTimer - dt);
    }

    public void ForceStopQuickBoost()
    {
        isQuickBoosting = false;
    }

    // Called from PlayerControls input callback.
    public void OnQuickBoost(float moveInputDirection, int facingDirection, bool anyFlyInputHeld, bool groundedNow)
    {
        // Queue a chain if already QBing.
        if (isQuickBoosting)
        {
            // small interval prevents double-queue from one input event or weird repeats
            if (qbChainIntervalTimer > 0f) return;

            int direction = InputUtils.AxisToDir(moveInputDirection);
            if (direction == 0) direction = facingDirection != 0 ? facingDirection : 1;

            qbChainQueued = true;
            qbQueuedDir = direction;
            qbChainBufferTimer = settings.qbChainBufferTime;
            qbChainIntervalTimer = settings.qbChainMinInterval;

            return;
        }

        if (quickBoostCooldownTimer > 0f) return;

        int directionStart = InputUtils.AxisToDir(moveInputDirection);
        // Safety: if somehow facingDirection is 0, default to right
        if (directionStart == 0) directionStart = facingDirection != 0 ? facingDirection : 1;

        quickBoostDir = directionStart;
        quickBoostTimer = 0f;
        isQuickBoosting = true;
        quickBoostCooldownTimer = settings.quickBoostCooldown;

        wasFlyingBeforeQuickBoost = (!groundedNow && anyFlyInputHeld);

        rb.gravityScale = 0f;

        // Optionally wipe horizontal velocity for crisp dash start and lock vertical.
        rb.linearVelocity = new Vector2(
          settings.wipeHorizontalOnQuickBoostStart ? 0f : rb.linearVelocity.x,
          0f
        );

        qbChainQueued = false;
        qbChainBufferTimer = 0f;
        qbChainIntervalTimer = settings.qbChainMinInterval;
    }

    // Called from PlayerControls.FixedUpdate when isQuickBoosting is true.
    public void DoQuickBoostStep(float moveInputDirection, int facingDirection, bool anyFlyInputHeld, bool groundedNow)
    {
        // chaining -> apply velocity -> check end
        if (HandleQBChaining()) return;
        ApplyQBVelocity(moveInputDirection);
        CheckQBEnd(anyFlyInputHeld, groundedNow);
    }

    // If a chain request exists and timing meets threshold, start the new QB immediately.
    private bool HandleQBChaining()
    {
        float dashPercentProgress = Mathf.Clamp01(quickBoostTimer / settings.quickBoostDuration);
        if (qbChainQueued && qbChainBufferTimer > 0f && dashPercentProgress >= settings.qbChainStartPercent)
        {
            quickBoostDir = qbQueuedDir;
            quickBoostTimer = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

            qbChainQueued = false;
            qbChainBufferTimer = 0f;
            qbChainIntervalTimer = settings.qbChainMinInterval;

            return true;
        }

        return false;
    }

    // Apply per-frame QB velocity using curve + accel/decel rules.
    private void ApplyQBVelocity(float moveInputDirection)
    {
        quickBoostTimer += Time.fixedDeltaTime;

        int heldDir = InputUtils.AxisToDir(moveInputDirection);
        rb.gravityScale = 0f;
        float timeRemainingPercentage = Mathf.Clamp01(quickBoostTimer / settings.quickBoostDuration);

        float curveMultiplier = settings.quickBoostCurve.Evaluate(timeRemainingPercentage);
        curveMultiplier = Mathf.Max(curveMultiplier, settings.quickBoostMinMultiplier);

        float targetSpeedAbs = settings.quickBoostStartSpeed * curveMultiplier;

        // If player holds dash direction, never drop below normal move speed during QB.
        if (heldDir != 0 && heldDir == quickBoostDir)
            targetSpeedAbs = Mathf.Max(targetSpeedAbs, CurrentHorizontalMoveSpeed());

        float targetVelocity = quickBoostDir * targetSpeedAbs;
        float currentVelocity = rb.linearVelocity.x;

        float rate;
        if (heldDir != 0 && heldDir == quickBoostDir)
            rate = settings.quickBoostAccel;
        else
            rate = (Mathf.Abs(targetVelocity) > Mathf.Abs(currentVelocity)) ? settings.quickBoostAccel : settings.quickBoostDecel;

        float newVx = Mathf.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newVx, 0f);
    }

    // Check for QB->fly release or QB end and handle exit.
    private void CheckQBEnd(bool anyFlyInputHeld, bool groundedNow)
    {
        float timeRemainingPercentage = Mathf.Clamp01(quickBoostTimer / settings.quickBoostDuration);
        bool wantsFly = anyFlyInputHeld && !groundedNow;

        if (wantsFly && timeRemainingPercentage >= settings.qbFlyReleasePercent)
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
        qbFlyCarryTimer = settings.qbFlyCarryTime;

        rb.gravityScale = wantsFly ? flightSettings.flyGravityScale : flightSettings.normalGravityScale;

        int currentMovingDir = InputUtils.AxisToDir(rb.linearVelocity.x); // direction based on actual motion is OK here
        float exitVx;

        // If player continues holding dash direction, exit at move speed floor.
        // We don't have raw input here (PlayerControls will protect carry in HorizontalMotor).
        if (currentMovingDir != 0 && currentMovingDir == quickBoostDir)
            exitVx = quickBoostDir * CurrentHorizontalMoveSpeed();
        else
            exitVx = quickBoostDir * settings.quickBoostNeutralExitSpeed;

        // Keep the stronger of carried QB speed vs the chosen exit speed (prevents a hitch).
        if (Mathf.Abs(qbCarryVx) > Mathf.Abs(exitVx))
            exitVx = qbCarryVx;

        float exitVy = 0f;
        if (wantsFly)
        {
            // If we were already flying when QB started, keep full upkick.
            // Otherwise apply a smaller upkick so QB->fly feels continuous even from ground/fall.
            float mult = wasFlyingBeforeQuickBoost ? settings.quickBoostFlyExitUpVelocityFlightMultiplier : settings.quickBoostFlyExitUpVelocityNonFlightMultiplier;
            exitVy = settings.quickBoostFlyExitUpVelocity * mult;
        }

        rb.linearVelocity = new Vector2(exitVx, exitVy);

        wasFlyingBeforeQuickBoost = false;
        isQuickBoosting = false;
    }

    private float CurrentHorizontalMoveSpeed()
    {
        // This mirrors your original: boostHeld lives in PlayerControls, so QB uses the "move speed floor"
        // as the normal (walk) floor. If you want QB floor to respect boostHeld, we can wire it in.
        return moveSettings.maxUnboostedGroundSpeed;
    }
}