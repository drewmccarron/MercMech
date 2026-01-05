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
    [Header("Quick Boost Speed")]

    [Tooltip("Initial dash speed (peak velocity).\nSuggested range: 15-25")]
    public float quickBoostStartSpeed = 20f;

    [Tooltip("Dash duration in seconds (full boost window).\nSuggested range: 0.3-0.6")]
    public float quickBoostDuration = 0.5f;

    [Tooltip("Speed-over-time curve (0=start, 1=end). Defines speed falloff during dash.")]
    public AnimationCurve quickBoostCurve = null;

    [Tooltip("Cooldown between dashes in seconds.\nSuggested range: 0.3-0.6")]
    public float quickBoostCooldown = 0.6f;

    [Tooltip("Flat energy cost when starting a quick boost.\nSuggested range: 20-35")]
    public float quickBoostCost = 25f;

    [Header("Quick Boost Acceleration")]

    [Tooltip("Ramps up toward target QB speed. Higher = snappier dash start.\nSuggested range: 150-300")]
    public float quickBoostAccel = 200f;

    [Tooltip("Ramps down during tail. Higher = quicker decel at end.\nSuggested range: 200-400")]
    public float quickBoostDecel = 260f;

    [Header("QB -> Fly Transition")]

    [Tooltip("Horizontal exit speed when no input on dash exit (neutral exit).\nSuggested range: 1-4")]
    public float quickBoostNeutralExitSpeed = 2f;

    [Header("QB Chaining")]

    [Tooltip("How long a QB press is remembered while QB is active (chain buffer window).\nSuggested range: 0.15-0.3")]
    public float qbChainBufferTime = 0.2f;

    [Tooltip("Earliest percent of dash where chaining into new QB is allowed (0-1).\nSuggested range: 0.7-0.85")]
    [Range(0f, 1f)]
    public float qbChainStartPercent = 0.8f;
  }

  // State
  public bool IsQuickBoosting { get; private set; }
  private float quickBoostTimer;
  private float cooldownTimer;
  private int dashDirection; // -1 or +1

  // Chaining
  private int queuedChainDirection;
  private bool hasQueuedChain;
  private float qbChainBufferTimer;

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
  }

  public void OnQuickBoost(float moveInputDirection, int facingDirection, bool anyFlyInputHeld, bool groundedNow, EnergyPool energyPool)
  {
    // Try to spend energy first - if can't afford, don't start QB.
    if (energyPool != null && !energyPool.TrySpend(settings.quickBoostCost))
      return;

    int direction = DetermineDirection(moveInputDirection, facingDirection);

    // If already boosting, queue a chain.
    if (IsQuickBoosting)
    {
      TryQueueChain(direction);
      return;
    }

    // Check cooldown.
    if (cooldownTimer > 0f)
      return;

    StartQuickBoost(direction, anyFlyInputHeld, groundedNow);
  }

  public void DoQuickBoostStep(float moveInputDirection, int facingDirection, bool anyFlyInputHeld, float currentMaxSpeed)
  {
    // Try to execute queued chain first.
    if (TryExecuteQueuedChain())
      return;

    // Apply velocity (pass the max speed)
    ApplyQuickBoostVelocity(moveInputDirection, currentMaxSpeed);

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
    bool isBufferActive = qbChainBufferTimer > 0f;
    if (isBufferActive)
      return;

    queuedChainDirection = direction;
    hasQueuedChain = true;
    qbChainBufferTimer = settings.qbChainBufferTime;
  }

  private void StartQuickBoost(int direction, bool anyFlyInputHeld, bool groundedNow)
  {
    dashDirection = direction;
    quickBoostTimer = 0f;
    IsQuickBoosting = true;
    cooldownTimer = settings.quickBoostCooldown;

    // Kill vertical momentum on start only.
    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

    // Clear chain state.
    hasQueuedChain = false;
  }

  private bool TryExecuteQueuedChain()
  {
    if (!hasQueuedChain)
      return false;

    float dashProgress = Mathf.Clamp01(quickBoostTimer / settings.quickBoostDuration);
    if (dashProgress < settings.qbChainStartPercent)
      return false;

    // Execute chain: restart dash in queued direction and kill vertical momentum again.
    dashDirection = queuedChainDirection;
    quickBoostTimer = 0f;
    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
    cooldownTimer = settings.quickBoostCooldown;

    hasQueuedChain = false;
    return true;
  }

  private void ApplyQuickBoostVelocity(float moveInputDirection, float currentMaxSpeed)
  {
    quickBoostTimer += Time.fixedDeltaTime;

    int heldDir = InputUtils.AxisToDir(moveInputDirection);

    // Calculate target speed from curve.
    float dashProgress = Mathf.Clamp01(quickBoostTimer / settings.quickBoostDuration);
    float curveMultiplier = settings.quickBoostCurve.Evaluate(dashProgress);
    float targetSpeedAbs = settings.quickBoostStartSpeed * curveMultiplier;

    // If player holds dash direction, floor speed at current movement max (respects boost/flight state).
    bool holdingDashDirection = heldDir != 0 && heldDir == dashDirection;
    if (holdingDashDirection)
      targetSpeedAbs = Mathf.Max(targetSpeedAbs, currentMaxSpeed);

    float targetVelocity = dashDirection * targetSpeedAbs;
    float currentVelocity = rb.linearVelocity.x;

    // Choose acceleration or deceleration rate.
    bool accelerating = Mathf.Abs(targetVelocity) > Mathf.Abs(currentVelocity);
    float rate = holdingDashDirection || accelerating
      ? settings.quickBoostAccel
      : settings.quickBoostDecel;

    float newVx = Mathf.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);
    
    // Only modify horizontal velocity - preserve vertical movement.
    rb.linearVelocity = new Vector2(newVx, rb.linearVelocity.y);
  }

  private void CheckQuickBoostEnd(bool anyFlyInputHeld)
  {
    float dashProgress = Mathf.Clamp01(quickBoostTimer / settings.quickBoostDuration);

    // Normal exit when duration complete
    if (dashProgress >= 1f)
        IsQuickBoosting = false;
    }
}