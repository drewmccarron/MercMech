using UnityEngine;
using UnityEngine.InputSystem;
using MercMech.Common;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerControls : MonoBehaviour
{
  // Cached components
  private Rigidbody2D rb;
  private Actions controls;
  private Collider2D col;
  
  public Rigidbody2D Rigidbody => rb;

  // Motors / systems
  private GroundProbe2D groundProbe;
  private HorizontalMotor2D horizontalMotor;
  private FlightMotor2D flightMotor;
  private QuickBoostMotor2D quickBoostMotor;
  private AimMotor2D aimMotor;

  public GroundProbe2D GroundProbe => groundProbe;
  
  // Player stats
  private PlayerStats playerStats;
  private EnergyPool energyPool;

  // Horizontal input (-1..1)
  private float moveInputDirection;

  #region Acceleration settings (Inspector -> HorizontalMotor2D.Settings)
  [Header("Acceleration")]
  [SerializeField] private HorizontalMotor2D.Settings horizontalSettings = new HorizontalMotor2D.Settings();
  #endregion

  #region Jump settings (Inspector -> JumpMotor2D.Settings)
  [Header("Jump")]
  [SerializeField] private JumpMotor2D.Settings jumpSettings = new JumpMotor2D.Settings();
  private JumpMotor2D jumpMotor;
  #endregion

  #region Ground probe settings (Inspector -> GroundProbe2D.Settings)
  [Header("Ground Probe")]
  [SerializeField] private GroundProbe2D.Settings groundProbeSettings = new GroundProbe2D.Settings();
  #endregion

  #region Boost / Fly settings (Inspector -> FlightMotor2D.Settings)
  [Header("Boost")]
  private bool boostHeld;
  public bool BoostHeld => boostHeld;

  [Header("Fly")]
  [SerializeField] private FlightMotor2D.Settings flightSettings = new FlightMotor2D.Settings();
  #endregion

  #region Quick boost (dash) settings (Inspector -> QuickBoostMotor2D.Settings)
  [Header("Quick Boost")]
  [SerializeField] private QuickBoostMotor2D.Settings quickBoostSettings = new QuickBoostMotor2D.Settings();
  #endregion

  [Header("Fall")]
  [SerializeField, Tooltip("Maximum falling speed (vertical). This clamps downward velocity.\nSuggested range: 6 - 14")]
  private FallSettings fallSettings = new FallSettings();
  [System.Serializable]
  private class FallSettings
  {
    [Tooltip("Max downward fall speed clamp.\nSuggested range: 6 - 14")]
    public float maxFallSpeed = 7f;
  }

  // Fire input
  private bool fireHeld;
  private bool firePressedThisFrame;

  public bool FireHeld => fireHeld;
  public bool FirePressedThisFrame => firePressedThisFrame;

  // Facing / state
  private int facingDirection = 1;
  public int FacingDirection => facingDirection;

  // Input tracking
  private bool flyKeyHeld;
  private bool jumpKeyHeld;
  private bool anyFlyInputHeld => jumpKeyHeld || flyKeyHeld;

  // Expose aim state for other systems (shooting, UI, etc.)
  public Vector2 AimWorldPosition => aimMotor != null ? aimMotor.AimWorldPosition : (Vector2)transform.position;
  public Vector2 AimDirection => aimMotor != null ? aimMotor.AimDirection : Vector2.right;
  public Vector2 AimOriginWorld => rb != null ? rb.worldCenterOfMass : (Vector2)transform.position;

  // Expose movement state for other systems (VFX, UI, etc.)
  public bool IsGrounded { get; private set; }
  public bool IsFlying => flightMotor.IsFlying;
  public bool IsBoosting => horizontalMotor.IsBoosting;
  public bool IsQuickBoosting => quickBoostMotor.IsQuickBoosting;

  // Expose energy state for other systems (UI, VFX, etc.)
  public float EnergyCurrent => energyPool.CurrentEnergy;
  public float EnergyMax => energyPool.MaxEnergy;

  // Expose ground probe for debug rendering
 

  // ------------------------
  // Unity lifecycle methods
  // ------------------------

  // Read non-physics inputs and update frame-based cooldowns.
  void Update()
  {
    // Read horizontal axis through Input System (guarded if controls not created).
    moveInputDirection = controls != null ? controls.Player.Walk.ReadValue<float>() : 0f;

    // Update visual/logic facing based on input
    int dir = InputUtils.AxisToDir(moveInputDirection);
    if (dir != 0) facingDirection = dir;

    firePressedThisFrame = false;
  }

  private void Reset()
  {
    if (groundProbeSettings.groundLayer == 0)
      groundProbeSettings.groundLayer = LayerMask.GetMask("World");
  }

  // Cache components and defaults.
  void Awake()
  {
    // Initialize input system
    controls = new Actions();
    rb = GetComponent<Rigidbody2D>();
    col = GetComponent<Collider2D>();
    playerStats = GetComponent<PlayerStats>();
    energyPool = playerStats != null ? playerStats.Energy : GetComponent<EnergyPool>();

    // Build systems (plain C# classes)
    groundProbe = new GroundProbe2D(col, groundProbeSettings);
    horizontalMotor = new HorizontalMotor2D(rb, horizontalSettings);
    flightMotor = new FlightMotor2D(rb, flightSettings);
    quickBoostMotor = new QuickBoostMotor2D(rb, quickBoostSettings, horizontalSettings, flightSettings);
    jumpMotor = new JumpMotor2D(rb, jumpSettings);
    aimMotor = new AimMotor2D();

    // Apply default gravity
    rb.gravityScale = flightSettings.normalGravityScale;

    // Ensure a safe default curve so Evaluate never NREs.
    if (quickBoostSettings.quickBoostCurve == null || quickBoostSettings.quickBoostCurve.length == 0)
    {
      quickBoostSettings.quickBoostCurve = new AnimationCurve(
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
      rb.gravityScale = flightSettings.normalGravityScale;
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

    controls.Player.Fire.started += OnFireStarted;
    controls.Player.Fire.canceled += OnFireCanceled;

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

      controls.Player.Fire.started -= OnFireStarted;
      controls.Player.Fire.canceled -= OnFireCanceled;

      controls.Player.QuickBoost.performed -= OnQuickBoost;

      controls.Player.Disable();
    }
  }

  // Physics loop: timers, ground state, movement, flight, clamps.
  private void FixedUpdate()
  {
    TickFixedTimers();

    // Update aim (frame-based - matches mouse update rate)
    aimMotor.UpdateAim(
      originWorld: AimOriginWorld,
      cam: Camera.main
    );

    // Update ground state   
    IsGrounded = groundProbe.Evaluate(rb, out var debugInfo);

    // If quick boosting, QB motor fully owns velocity/gravity for this step.
    if (quickBoostMotor.IsQuickBoosting)
    {
      quickBoostMotor.DoQuickBoostStep(
        moveInputDirection: moveInputDirection,
        facingDirection: facingDirection,
        anyFlyInputHeld: anyFlyInputHeld
      );
      return; // skip normal movement while dashing
    }

    // Horizontal motor (uses QB carry protection values exposed by quickBoostMotor)
    horizontalMotor.ProcessHorizontalMovement(
      groundedNow: IsGrounded,
      moveInputDirection: moveInputDirection,
      boostHeld: boostHeld,
      qbFlyCarryTimer: quickBoostMotor.qbFlyCarryTimer,
      qbCarryVx: quickBoostMotor.qbCarryVx,
      isFlying: flightMotor.IsFlying
    );

    // Flight motor (only process when not winding up for jump)
    if (!jumpMotor.IsWindingUp)
    {
      // If we're already flying, drain energy. If not flying, check if we can start flight.
      bool hasEnergyForFlight = flightMotor.IsFlying ? energyPool.HasEnergy : energyPool.CanStartFlight();

      flightMotor.ProcessFlight(
        anyFlyInputHeld: anyFlyInputHeld,
        jumpedFromGround: ref jumpMotor.jumpedFromGround,
        hasEnergyForFlight: hasEnergyForFlight
      );
    }

    // Energy tick: regen/drain depends on grounded, flying, and quick boost state.
    energyPool.TickEnergy(
      groundedNow: IsGrounded,
      boostHeld: boostHeld,
      isFlying: flightMotor.IsFlying,
      dt: Time.fixedDeltaTime
    );

    ClampFallSpeed();
  }

  // ------------------------
  // Helper / small methods
  // ------------------------

  // Centralized fixed-timestep timer updates to avoid duplication.
  private void TickFixedTimers()
  {
    float dt = Time.fixedDeltaTime;

    if (jumpMotor != null)
      jumpMotor.TickFixedTimers(dt);

    if (quickBoostMotor != null)
      quickBoostMotor.TickFixedTimers(dt);
  }

  // Prevent infinite falling velocity.
  private void ClampFallSpeed()
  {
    if (rb.linearVelocity.y < -fallSettings.maxFallSpeed)
      rb.linearVelocity = new Vector2(rb.linearVelocity.x, -fallSettings.maxFallSpeed);
  }

  // ------------------------
  // Input callbacks
  // ------------------------

  private void OnJumpStarted(InputAction.CallbackContext ctx)
  {
    jumpKeyHeld = true;

    if (jumpMotor != null && IsGrounded)
      jumpMotor.OnJumpStarted();
  }

  private void OnJumpCanceled(InputAction.CallbackContext ctx)
  {
    jumpKeyHeld = false;

    if (jumpMotor != null)
      jumpMotor.OnJumpCanceled();
  }

  private void OnQuickBoost(InputAction.CallbackContext ctx)
  {
    quickBoostMotor.OnQuickBoost(
      moveInputDirection: moveInputDirection,
      facingDirection: facingDirection,
      anyFlyInputHeld: anyFlyInputHeld,
      groundedNow: IsGrounded,
      energyPool: energyPool
    );
  }

  private void OnFlyStarted(InputAction.CallbackContext ctx) => flyKeyHeld = true;
  private void OnFlyCanceled(InputAction.CallbackContext ctx) => flyKeyHeld = false;

  private void OnBoostStarted(InputAction.CallbackContext ctx)
  {
    // Upfront horizontal boost cost (applies whether grounded or airborne).
    if (!energyPool.CanStartBoost())
    {
      boostHeld = false;
      return;
    }

    boostHeld = true;
  }
  private void OnBoostCanceled(InputAction.CallbackContext ctx) => boostHeld = false;

  private void OnFireStarted(InputAction.CallbackContext ctx)
  {
    fireHeld = true;
    firePressedThisFrame = true;
  }

  private void OnFireCanceled(InputAction.CallbackContext ctx) => fireHeld = false;
}