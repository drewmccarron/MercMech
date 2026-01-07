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
    private MovementDebugSamples movementDebug;
    public MovementDebugSamples MovementDebug => movementDebug;

    public GroundProbe2D GroundProbe => groundProbe;

    // Player stats
    private PlayerStats playerStats;
    private EnergyPool energyPool;

    // Horizontal input (-1..1)
    private float moveInputDirection;

    #region Acceleration settings (Inspector -> HorizontalMotor2D.Settings)
    [Header("Horizontal Movement")]
    [SerializeField, Tooltip("Horizontal acceleration and speed settings (walk, boost, air control).")]
    private HorizontalMotor2D.Settings horizontalSettings = new HorizontalMotor2D.Settings();
    #endregion

    #region Jump settings (Inspector -> JumpMotor2D.Settings)
    [Header("Jump")]
    [SerializeField, Tooltip("Jump mechanics (force, coyote time, buffer, windup).")]
    private JumpMotor2D.Settings jumpSettings = new JumpMotor2D.Settings();
    private JumpMotor2D jumpMotor;
    #endregion

    #region Ground probe settings (Inspector -> GroundProbe2D.Settings)
    [Header("Ground Detection")]
    [SerializeField, Tooltip("Ground detection settings (layer mask, probe dimensions).")]
    private GroundProbe2D.Settings groundProbeSettings = new GroundProbe2D.Settings();
    #endregion

    #region Boost / Fly settings (Inspector -> FlightMotor2D.Settings)
    [Header("Boost")]
    private bool boostHeld;
    public bool BoostHeld => boostHeld;

    [Header("Flight")]
    [SerializeField, Tooltip("Flight mechanics (upward thrust, gravity, speed caps).")]
    private FlightMotor2D.Settings flightSettings = new FlightMotor2D.Settings();
    #endregion

    #region Quick boost (dash) settings (Inspector -> QuickBoostMotor2D.Settings)
    [Header("Quick Boost (Dash)")]
    [SerializeField, Tooltip("Quick boost/dash mechanics (speed, duration, chaining, energy cost).")]
    private QuickBoostMotor2D.Settings quickBoostSettings = new QuickBoostMotor2D.Settings();
    #endregion

    [Header("Fall Speed Limit")]
    [SerializeField, Tooltip("Maximum falling speed (vertical terminal velocity).\nArmored Core feel: 10-16")]
    private FallSettings fallSettings = new FallSettings();
    [System.Serializable]
    private class FallSettings
    {
        [Tooltip("Max downward fall speed clamp (terminal velocity).\nArmored Core feel: 10-16")]
        public float maxFallSpeed = 12f;
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

    // Expose resource state for other systems (UI, VFX, etc.)
    public float EnergyCurrent => energyPool.CurrentEnergy;
    public float EnergyMax => energyPool.MaxEnergy;

    public float HealthCurrent => playerStats.Health.CurrentHealth;
    public float HealthMax => playerStats.Health.MaxHealth;

    // Expose params for boost particle effects
    public float QuickBoostStrength01 => quickBoostMotor != null ? quickBoostMotor.QBStrength01 : 0f;
    public float FlyThrottle01 => flightMotor != null && flightMotor.IsFlying ? flightMotor.FlyThrottle01 : 0f;

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
        movementDebug = new MovementDebugSamples();


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
        float dt = Time.fixedDeltaTime;
        TickFixedTimers(dt);

        // Update aim (frame-based - matches mouse update rate)
        aimMotor.UpdateAim(
          originWorld: AimOriginWorld,
          cam: Camera.main
        );

        // Update ground state   
        IsGrounded = groundProbe.Evaluate(rb, out var debugInfo);

        // If quick boosting, QB motor controls horizontal velocity only.
        if (quickBoostMotor.IsQuickBoosting)
        {
            // Calculate what the max speed would be for current state
            float currentMaxSpeed = horizontalMotor.GetCurrentMaxSpeed(
              IsGrounded,
              flightMotor.IsFlying
            );

            quickBoostMotor.DoQuickBoostStep(
              moveInputDirection: moveInputDirection,
              facingDirection: facingDirection,
              anyFlyInputHeld: anyFlyInputHeld,
              currentMaxSpeed: currentMaxSpeed
            );

            if (quickBoostMotor.QBPercentComplete < quickBoostSettings.quickBoostPercentToFlyTransition)
            {
                movementDebug.Sample(rb, Time.fixedDeltaTime);
                ClampFallSpeed();
                return; // skip normal horizontal movement and energy drain while dashing
            }

            // Allow flight to work during quick boost (vertical movement).
            if (!jumpMotor.IsWindingUp)
            {
                bool hasEnergyForFlight = flightMotor.IsFlying ? energyPool.HasEnergy : energyPool.CanStartFlight();

                flightMotor.ProcessFlight(
                  anyFlyInputHeld: anyFlyInputHeld,
                  jumpedFromGround: ref jumpMotor.jumpedFromGround,
                  hasEnergyForFlight: hasEnergyForFlight,
                  dt: dt
                );
            }

            movementDebug.Sample(rb, Time.fixedDeltaTime);
            ClampFallSpeed();
            return; // skip normal horizontal movement and energy drain while dashing
        }

        // Normal movement (not quick boosting)
        horizontalMotor.ProcessHorizontalMovement(
          groundedNow: IsGrounded,
          moveInputDirection: moveInputDirection,
          boostHeld: boostHeld,
          isFlying: flightMotor.IsFlying,
          dt: dt
        );

        // Flight motor (only process when not winding up for jump)
        if (!jumpMotor.IsWindingUp)
        {
            // If we're already flying, drain energy. If not flying, check if we can start flight.
            bool hasEnergyForFlight = flightMotor.IsFlying ? energyPool.HasEnergy : energyPool.CanStartFlight();

            flightMotor.ProcessFlight(
              anyFlyInputHeld: anyFlyInputHeld,
              jumpedFromGround: ref jumpMotor.jumpedFromGround,
              hasEnergyForFlight: hasEnergyForFlight,
              dt: dt
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

        movementDebug.Sample(rb, Time.fixedDeltaTime);

    }

    // ------------------------
    // Helper / small methods
    // ------------------------

    // Centralized fixed-timestep timer updates to avoid duplication.
    private void TickFixedTimers(float dt)
    {
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