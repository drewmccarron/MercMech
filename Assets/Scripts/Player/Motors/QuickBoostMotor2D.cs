using MercMech.Common;
using UnityEngine;

public class QuickBoostMotor2D
{
  private readonly Rigidbody2D rb;
  private readonly Settings settings;
  private readonly HorizontalMotor2D.Settings moveSettings;
  private readonly FlightMotor2D.Settings flightSettings;

  [System.Serializable]
  public class Settings
  {
    [Header("Quick Boost")]
    [Tooltip("Configured initial dash speed (units/sec).\nSuggested range: 8 - 32")]
    public float quickBoostStartSpeed = 20f;

    [Tooltip("Dash duration in seconds.\nSuggested range: 0.18 - 0.6")]
    public float quickBoostDuration = 0.5f;

    [Tooltip("Speed-over-time curve (evaluated 0->1 across the dash).")]
    public AnimationCurve quickBoostCurve = null;

    [Tooltip("Cooldown between dashes in seconds.\nSuggested range: 0.1 - 1.0")]
    public float quickBoostCooldown = 0.4f;

    [Tooltip("Flat energy cost when starting a quick boost.")]
    public float quickBoostCost = 25f;

    [Header("Quick Boost Acceleration")]
    [Tooltip("Ramps up toward the target QB speed. Higher = snappier accelerate.\nSuggested range: 50 - 400")]
    public float quickBoostAccel = 200f;

    [Tooltip("Ramps down during the tail. Higher = quicker tail decel.\nSuggested range: 100 - 600")]
    public float quickBoostDecel = 260f;

    [Header("QB -> Fly Transition")]
    [Tooltip("How long to protect QB horizontal momentum after QB ends (seconds).\nSuggested range: 0.05 - 0.4")]
    public float qbFlyCarryTime = 0.18f;

    [Tooltip("Percentage of dash completed before QB->fly is allowed.\n0..1")]
    [Range(0f, 1f)]
    public float qbFlyReleasePercent = 0.85f;

    [Tooltip("Upward velocity applied when exiting QB into flight.\nSuggested range: 4 - 14")]
    public float quickBoostFlyExitUpVelocity = 10f;

    [Tooltip("Horizontal exit speed when no input on dash exit.\nSuggested range: 0 - 4")]
    public float quickBoostNeutralExitSpeed = 2f;

    [Header("QB Chaining")]
    [Tooltip("How long a QB press is remembered while QB is active (seconds).\nSuggested range: 0.05 - 0.4")]
    public float qbChainBufferTime = 0.2f;
    [Tooltip("Earliest percent of the dash where chaining into a new QB is allowed (0..1).")]
    [Range(0f, 1f)]
    public float qbChainStartPercent = 0.8f;

    [Tooltip("Minimum interval between chain requests (prevents double-queue).\nSuggested range: 0.01 - 0.1")]
    public float qbChainMinInterval = 0.05f;

    [Header("Quick Boost -> Fly Upkick Multipliers")]
    [Tooltip("Upkick multiplier when QB->fly from ground/falling (not flying before QB). 0..1")]
    [Range(0f, 1f)]
    public float quickBoostFlyExitUpVelocityNonFlightMultiplier = 0.6f;

    [Tooltip("Upkick multiplier when QB->fly and already flying before QB. 0..1")]
    [Range(0f, 1f)]
    public float quickBoostFlyExitUpVelocityFlightMultiplier = 1f;
  }

  // State
  public bool IsQuickBoosting { get; private set; }
  private float quickBoostTimer;
  private float cooldownTimer;
  private int dashDirection; // -1 or +1

  // QB -> Fly carry protection
  public float qbFlyCarryTimer { get; private set; }
  public float qbCarryVx { get; private set; }

  // Chaining
  private int queuedChainDirection;
  private float lastChainRequestTime;
  private bool hasQueuedChain;
  private float qbChainBufferTimer;

  // Flight state tracking
  private bool wasFlyingBeforeQuickBoost;

  public QuickBoostMotor2D(Rigidbody2D rb, Settings settings, HorizontalMotor2D.Settings moveSettings, FlightMotor2D.Settings flightSettings)
  {
    this.rb = rb;
    this.settings = settings;
    this.moveSettings = moveSettings;
    this.flightSettings = flightSettings;
  }

  public void TickFixedTimers(float dt)
  {
    if (qbChainBufferTimer > 0f)
      qbChainBufferTimer = Mathf.Max(0f, qbChainBufferTimer - dt);

    if (cooldownTimer > 0f)
      cooldownTimer = Mathf.Max(0f, cooldownTimer - dt);

    if (qbFlyCarryTimer > 0f)
      qbFlyCarryTimer = Mathf.Max(0f, qbFlyCarryTimer - dt);
  }

  public void OnQuickBoost(float moveInputDirection, int facingDirection, bool anyFlyInputHeld, bool groundedNow, EnergyPool energyPool)
  {
    // Try to spend energy first - if we can't afford it, don't start QB
    if (energyPool != null && !energyPool.TrySpend(settings.quickBoostCost))
      return;
    
    int direction = DetermineDirection(moveInputDirection, facingDirection);

    // If already boosting, queue a chain
    if (IsQuickBoosting)
    {
      TryQueueChain(direction);
      return;
    }

    // Check cooldown
    if (cooldownTimer > 0f)
      return;

    StartQuickBoost(direction, anyFlyInputHeld, groundedNow);
  }

  public void DoQuickBoostStep(float moveInputDirection, int facingDirection, bool anyFlyInputHeld)
  {
    // Try to execute queued chain first
    if (TryExecuteQueuedChain())
      return;

    // Apply velocity
    ApplyQuickBoostVelocity(moveInputDirection);

    // Check for exit conditions
    CheckQuickBoostEnd(anyFlyInputHeld);
  }

  // -------------------------
  // Private Helper Methods
  // -------------------------

  private int DetermineDirection(float moveInputDirection, int facingDirection)
  {
    int direction = InputUtils.AxisToDir(moveInputDirection);
    if (direction == 0)
      direction = facingDirection != 0 ? facingDirection : 1;
    return direction;
  }

  private void TryQueueChain(int direction)
  {
    // Prevent double-queue via minimum interval
    float timeSinceLastRequest = Time.fixedTime - lastChainRequestTime;
    bool bufferActive = qbChainBufferTimer > 0f;
    if (timeSinceLastRequest < settings.qbChainMinInterval && bufferActive)
      return;

    queuedChainDirection = direction;
    hasQueuedChain = true;
    lastChainRequestTime = Time.fixedTime;
  }

  private void StartQuickBoost(int direction, bool anyFlyInputHeld, bool groundedNow)
  {
    dashDirection = direction;
    quickBoostTimer = 0f;
    IsQuickBoosting = true;
    cooldownTimer = settings.quickBoostCooldown;
    wasFlyingBeforeQuickBoost = !groundedNow && anyFlyInputHeld;

    // Lock gravity and vertical velocity
    rb.gravityScale = 0f;
    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

    // Clear chain state
    hasQueuedChain = false;
    lastChainRequestTime = Time.fixedTime;
  }

  private bool TryExecuteQueuedChain()
  {
    if (!hasQueuedChain)
      return false;

    float dashProgress = Mathf.Clamp01(quickBoostTimer / settings.quickBoostDuration);
    if (dashProgress < settings.qbChainStartPercent)
      return false;

    // Execute chain: restart dash in queued direction
    dashDirection = queuedChainDirection;
    quickBoostTimer = 0f;
    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

    hasQueuedChain = false;
    lastChainRequestTime = Time.fixedTime;

    return true;
  }

  private void ApplyQuickBoostVelocity(float moveInputDirection)
  {
    quickBoostTimer += Time.fixedDeltaTime;

    int heldDir = InputUtils.AxisToDir(moveInputDirection);
    rb.gravityScale = 0f;

    // Calculate target speed from curve
    float dashProgress = Mathf.Clamp01(quickBoostTimer / settings.quickBoostDuration);
    float curveMultiplier = settings.quickBoostCurve.Evaluate(dashProgress);
    float targetSpeedAbs = settings.quickBoostStartSpeed * curveMultiplier;

    // If player holds dash direction, floor speed at walk speed
    bool holdingDashDirection = heldDir != 0 && heldDir == dashDirection;
    if (holdingDashDirection)
      targetSpeedAbs = Mathf.Max(targetSpeedAbs, moveSettings.maxUnboostedGroundSpeed);

    float targetVelocity = dashDirection * targetSpeedAbs;
    float currentVelocity = rb.linearVelocity.x;

    // Choose acceleration or deceleration rate
    bool accelerating = Mathf.Abs(targetVelocity) > Mathf.Abs(currentVelocity);
    float rate = holdingDashDirection || accelerating 
      ? settings.quickBoostAccel 
      : settings.quickBoostDecel;

    float newVx = Mathf.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);
    rb.linearVelocity = new Vector2(newVx, 0f);
  }

  private void CheckQuickBoostEnd(bool anyFlyInputHeld)
  {
    float dashProgress = Mathf.Clamp01(quickBoostTimer / settings.quickBoostDuration);

    // Early exit into flight if conditions met
    if (anyFlyInputHeld && dashProgress >= settings.qbFlyReleasePercent)
    {
      EndQuickBoost(true);
      return;
    }

    // Normal exit when duration complete
    if (dashProgress >= 1f)
    {
      EndQuickBoost(anyFlyInputHeld);
    }
  }

  private void EndQuickBoost(bool wantsFly)
  {
    // Capture current velocity for carry protection
    qbCarryVx = rb.linearVelocity.x;
    qbFlyCarryTimer = settings.qbFlyCarryTime;

    // Set gravity based on flight intent
    rb.gravityScale = wantsFly 
      ? flightSettings.flyGravityScale 
      : flightSettings.normalGravityScale;

    // Determine exit horizontal velocity
    int currentMovingDir = InputUtils.AxisToDir(rb.linearVelocity.x);
    bool stillMovingInDashDirection = currentMovingDir != 0 && currentMovingDir == dashDirection;

    float exitVx = stillMovingInDashDirection
      ? dashDirection * moveSettings.maxUnboostedGroundSpeed
      : dashDirection * settings.quickBoostNeutralExitSpeed;

    // Keep stronger of carried speed vs exit speed (prevents hitching)
    if (Mathf.Abs(qbCarryVx) > Mathf.Abs(exitVx))
      exitVx = qbCarryVx;

    // Calculate vertical exit velocity if transitioning to flight
    float exitVy = 0f;
    if (wantsFly)
    {
      float upkickMultiplier = wasFlyingBeforeQuickBoost
        ? settings.quickBoostFlyExitUpVelocityFlightMultiplier
        : settings.quickBoostFlyExitUpVelocityNonFlightMultiplier;

      exitVy = settings.quickBoostFlyExitUpVelocity * upkickMultiplier;
    }

    rb.linearVelocity = new Vector2(exitVx, exitVy);

    // Reset state
    IsQuickBoosting = false;
    wasFlyingBeforeQuickBoost = false;
  }
}