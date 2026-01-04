using MercMech.Common;
using UnityEngine;

public class HorizontalMotor2D
{
  private readonly Rigidbody2D rb;

  [System.Serializable]
  public class Settings
  {
    [Header("Walk Acceleration")]

    [Tooltip("Ground acceleration when walking/moving. Higher = faster reach of target speed.\nArmored Core feel: 30-60")]
    public float groundWalkAccel = 30f;

    [Tooltip("Ground deceleration when releasing movement input. Higher = stops quicker.\nArmored Core feel: 20-40")]
    public float groundBrakeDecel = 15f;

    [Tooltip("Ground reverse acceleration (turning/direction change). Higher = faster reversals.\nArmored Core feel: 40-80")]
    public float groundTurnAccel = 30f;

    [Tooltip("Max horizontal walk speed (unboosted) on ground.\nArmored Core feel: 4-6")]
    public float maxUnboostedGroundSpeed = 5f;

    [Header("Airborne Acceleration")]

    [Tooltip("Air control acceleration when providing horizontal input. Higher = stronger air steering.\nArmored Core feel: 10-25")]
    public float airAccel = 15f;

    [Tooltip("Air drag when no horizontal input (passive slowdown). Higher = faster slowdown.\nArmored Core feel: 3-8")]
    public float airDecel = 5f;

    [Tooltip("Air reverse acceleration (turning while airborne). Higher = faster air reversals.\nArmored Core feel: 15-35")]
    public float airTurnAccel = 25f;

    [Tooltip("Extra deceleration in-air when releasing all input AND not boosting (active air brake).\nArmored Core feel: 25-50")]
    public float airBrakeDecel = 35f;

    [Tooltip("Maximum horizontal speed while falling (unboosted, not flying).\nSuggested range: 3-5")]
    public float maxFallingHorizontalSpeed = 4f;

    [Header("Boost Settings")]

    [Tooltip("Max horizontal speed when boosting on ground.\nSuggested range: 8-12")]
    public float maxGroundBoostSpeed = 10f;

    [Tooltip("Maximum horizontal speed while falling AND boosting (airborne, not flying).\nSuggested range: 6-10")]
    public float maxFallingBoostSpeed = 8f;

    [Header("Boost Acceleration Multipliers")]

    [Tooltip("Multiplier applied to acceleration/thrust while boost is held.\nSuggested range: 1.3-2.0")]
    public float boostAccelMultiplier = 1.6f;

    [Tooltip("Multiplier applied when reversing direction with boost held.\nSuggested range: 1.5-2.5")]
    public float boostTurnAccelMultiplier = 1.9f;

    [Header("Flight Speed")]

    [Tooltip("Maximum horizontal speed while flying (independent of boost - flight uses energy instead).\nSuggested Range: 5-8")]
    public float maxFlyingSpeed = 6f;
  }

  private readonly Settings settings;

  // Expose boost state for consistency with IsFlying, IsQuickBoosting
  public bool IsBoosting { get; private set; }

  public HorizontalMotor2D(Rigidbody2D rb, Settings settings)
  {
    this.rb = rb;
    this.settings = settings;
  }

  // Process horizontal movement: applies acceleration, caps speed, handles QB carry protection.
  public void ProcessHorizontalMovement(
    bool groundedNow,
    float moveInputDirection,
    bool boostHeld,
    float qbFlyCarryTimer,
    float qbCarryVx,
    bool isFlying,
    float dt)
  {
    // Update boost state based on input
    IsBoosting = boostHeld;

    float maxSpeed = GetCurrentMaxSpeed(groundedNow, isFlying);
    float targetVelocity = moveInputDirection * maxSpeed;
    float currentVelocity = rb.linearVelocity.x;

    // Protect QB carry: prevent normal movement from overriding QB exit momentum.
    bool protectCarry = qbFlyCarryTimer > 0f;
    int carryDir = InputUtils.AxisToDir(qbCarryVx);

    if (protectCarry)
    {
      targetVelocity = GetCarryVelocity(targetVelocity, carryDir, qbCarryVx);
    }

    bool hasInput = Mathf.Abs(moveInputDirection) > 0.001f;
    bool reversing = hasInput &&
                         Mathf.Sign(targetVelocity) != Mathf.Sign(currentVelocity) &&
                         Mathf.Abs(currentVelocity) > 0.1f;

    if (groundedNow)
    {
      float newVx = GetGroundVelocity(hasInput, reversing, currentVelocity, targetVelocity, dt);
      rb.linearVelocity = new Vector2(newVx, rb.linearVelocity.y);
    }
    else
    {
      // Air: apply thrust while input, otherwise apply drag.
      if (hasInput)
      {
        float newVx = GetAirVelocity(reversing, moveInputDirection, maxSpeed);
        rb.linearVelocity = new Vector2(newVx, rb.linearVelocity.y);
      }
      else
      {
        // No input: apply air drag toward 0.
        float newVx = GetAirDragVelocity(dt, isFlying);

        // QB carry protection: don't drag below carried QB speed.
        if (protectCarry && carryDir != 0)
        {
          if (carryDir > 0) newVx = Mathf.Max(newVx, qbCarryVx);
          else newVx = Mathf.Min(newVx, qbCarryVx);
        }

        rb.linearVelocity = new Vector2(newVx, rb.linearVelocity.y);
      }
    }
  }

  // Ground movement: deterministic MoveTowards-style acceleration.
  private float GetGroundVelocity(bool hasInput, bool reversing, float currentVelocity, float targetVelocity, float dt)
  {
    float accelRate;
    if (!hasInput) accelRate = settings.groundBrakeDecel;
    else if (reversing) accelRate = settings.groundTurnAccel;
    else accelRate = settings.groundWalkAccel;

    if (IsBoosting)
      accelRate *= reversing ? settings.boostTurnAccelMultiplier : settings.boostAccelMultiplier;

    return Mathf.MoveTowards(currentVelocity, targetVelocity, accelRate * dt);
  }

  // Air movement: apply thrust via AddForce, then cap speed.
  private float GetAirVelocity(bool reversing, float moveInputDirection, float maxSpeed)
  {
    // Thrust amount: airTurnAccel when reversing, otherwise airAccel.
    float thrust = reversing ? settings.airTurnAccel : settings.airAccel;

    if (IsBoosting)
      thrust *= reversing ? settings.boostTurnAccelMultiplier : settings.boostAccelMultiplier;

    // Apply horizontal thrust (ForceMode2D.Force = acceleration-like for a given mass).
    rb.AddForce(Vector2.right * moveInputDirection * thrust, ForceMode2D.Force);

    // Cap air speed to current maxSpeed (keeps things controllable).
    float vx = rb.linearVelocity.x;
    if (Mathf.Abs(vx) > maxSpeed)
      vx = Mathf.Sign(vx) * maxSpeed;

    return vx;
  }

  // QB carry protection: floor/ceiling target velocity to preserve QB exit momentum.
  private float GetCarryVelocity(float targetVelocity, int carryDir, float qbCarryVelocity)
  {
    int heldDirForCarry = InputUtils.AxisToDir(carryDir);
    if (heldDirForCarry == 0 || heldDirForCarry == carryDir)
    {
      if (carryDir > 0)
        targetVelocity = Mathf.Max(targetVelocity, qbCarryVelocity);
      else if (carryDir < 0)
        targetVelocity = Mathf.Min(targetVelocity, qbCarryVelocity);
    }

    return targetVelocity;
  }

  // Air drag: passive slowdown when no input. Faster if not boosting/flying (active air brake).
  private float GetAirDragVelocity(float dt, bool isFlying)
  {
    float dragDecel = settings.airDecel;
    bool canAirBrake = !IsBoosting && !isFlying;
    if (canAirBrake)
      dragDecel = settings.airBrakeDecel;

    float vx = rb.linearVelocity.x;
    return Mathf.MoveTowards(vx, 0f, dragDecel * dt);
  }

  // Single source of truth for horizontal speed caps based on current state.
  public float GetCurrentMaxSpeed(bool groundedNow, bool isFlying)
  {
    if (groundedNow)
      return IsBoosting ? settings.maxGroundBoostSpeed : settings.maxUnboostedGroundSpeed;

    if (isFlying)
      return settings.maxFlyingSpeed;

    return IsBoosting ? settings.maxFallingBoostSpeed : settings.maxFallingHorizontalSpeed;
  }
}
