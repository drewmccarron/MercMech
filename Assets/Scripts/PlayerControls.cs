using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerControls : MonoBehaviour
{
    // Cached components
    private Rigidbody2D rb;
    private Actions controls;
    private Collider2D col;

    // Horizontal input (-1..1)
    private float moveInputDirection;

    #region Acceleration settings (Inspector -> HorizontalMotor2D.Settings)
    [Header("Acceleration")]
    [SerializeField] private HorizontalMotor2D.Settings horizontalSettings = new HorizontalMotor2D.Settings();
    #endregion

    #region Move settings (Inspector -> HorizontalMotor2D.MoveSettings)
    [Header("Move")]
    [SerializeField] private HorizontalMotor2D.MoveSettings moveSettings = new HorizontalMotor2D.MoveSettings();
    public const float MoveDeadzone = 0.2f;
    #endregion

    #region Jump settings (Inspector -> JumpMotor2D.Settings)
    [Header("Jump")]
    [SerializeField] private JumpMotor2D.Settings jumpSettings = new JumpMotor2D.Settings();
    private JumpMotor2D jumpMotor;
    #endregion

    #region Boost / Fly settings (Inspector -> FlightMotor2D.Settings)
    [Header("Boost")]
    private bool boostHeld;

    [Header("Fly")]
    [SerializeField] private FlightMotor2D.Settings flightSettings = new FlightMotor2D.Settings();
    #endregion

    #region Quick boost (dash) settings (Inspector -> QuickBoostMotor2D.Settings)
    [Header("Quick Boost")]
    [SerializeField] private QuickBoostMotor2D.Settings quickBoostSettings = new QuickBoostMotor2D.Settings();
    #endregion

    [Header("Fall")]
    [SerializeField] private FallSettings fallSettings = new FallSettings();

    // Facing / state
    private int facingDirection = 1;

    // Input tracking
    private bool flyKeyHeld;
    private bool jumpKeyHeld;
    private bool anyFlyInputHeld => jumpKeyHeld || flyKeyHeld;

    // Motors / systems
    private GroundProbe2D groundProbe;
    private HorizontalMotor2D horizontalMotor;
    private FlightMotor2D flightMotor;
    private QuickBoostMotor2D quickBoostMotor;

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
        if (quickBoostMotor != null)
            quickBoostMotor.TickQuickBoostCooldown(Time.deltaTime);
    }

    // Cache components and defaults.
    void Awake()
    {
        // Initialize input system
        controls = new Actions();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        // Build systems (plain C# classes)
        groundProbe = new GroundProbe2D(col, jumpSettings.groundLayer);
        horizontalMotor = new HorizontalMotor2D(rb, horizontalSettings, moveSettings);
        flightMotor = new FlightMotor2D(rb, flightSettings);
        quickBoostMotor = new QuickBoostMotor2D(rb, quickBoostSettings, moveSettings, flightSettings);
        jumpMotor = new JumpMotor2D(rb, jumpSettings);

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
            rb.gravityScale = flightSettings.normalGravityScale;
            if (quickBoostMotor != null)
                quickBoostMotor.ForceStopQuickBoost();
        }
    }

    // Physics loop: timers, ground state, movement, flight, clamps.
    private void FixedUpdate()
    {
        TickFixedTimers();

        bool groundedNow = UpdateGroundState();

        // If quick boosting, QB motor fully owns velocity/gravity for this step.
        if (quickBoostMotor != null && quickBoostMotor.isQuickBoosting)
        {
            quickBoostMotor.DoQuickBoostStep(
                moveInputDirection: moveInputDirection,
                facingDirection: facingDirection,
                anyFlyInputHeld: anyFlyInputHeld,
                groundedNow: groundedNow
            );
            return; // skip normal movement while dashing
        }

        // Horizontal motor (uses QB carry protection values exposed by quickBoostMotor).
        float qbFlyCarryTimer = quickBoostMotor != null ? quickBoostMotor.qbFlyCarryTimer : 0f;
        float qbCarryVx = quickBoostMotor != null ? quickBoostMotor.qbCarryVx : 0f;

        horizontalMotor.ProcessHorizontalMovement(
            groundedNow: groundedNow,
            moveInputDirection: moveInputDirection,
            boostHeld: boostHeld,
            qbFlyCarryTimer: qbFlyCarryTimer,
            qbCarryVx: qbCarryVx
        );

        // Flight motor
        flightMotor.ProcessFlight(
            groundedNow: groundedNow,
            flyKeyHeld: flyKeyHeld,
            jumpKeyHeld: jumpKeyHeld,
            jumpedFromGround: ref jumpMotor.jumpedFromGround
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

    // Update grounded timers and return current grounded state.
    private bool UpdateGroundState()
    {
        bool groundedNow = groundProbe != null && groundProbe.IsGrounded();

        if (jumpMotor != null)
            jumpMotor.UpdateGroundState(groundedNow, Time.fixedDeltaTime);

        return groundedNow;
    }

    // Convert axis to -1/0/1 using the configured deadzone.
    private static int AxisToDir(float axis)
    {
        if (axis > MoveDeadzone) return 1;
        if (axis < -MoveDeadzone) return -1;
        return 0;
    }

    // Prevent infinite falling velocity.
    private void ClampFallSpeed()
    {
        if (rb.linearVelocity.y < -fallSettings.maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -fallSettings.maxFallSpeed);
    }

    // ------------------------
    // Jump callbacks & helpers
    // ------------------------

    private void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        jumpKeyHeld = true;

        if (jumpMotor != null)
            jumpMotor.OnJumpStarted();
    }

    private void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        jumpKeyHeld = false;

        if (jumpMotor != null)
            jumpMotor.OnJumpCanceled();
    }

    // ------------------------
    // Quick Boost (dash) input
    // ------------------------

    private void OnQuickBoost(InputAction.CallbackContext ctx)
    {
        if (quickBoostMotor == null) return;

        bool groundedNow = groundProbe != null && groundProbe.IsGrounded();

        quickBoostMotor.OnQuickBoost(
            moveInputDirection: moveInputDirection,
            facingDirection: facingDirection,
            anyFlyInputHeld: anyFlyInputHeld,
            groundedNow: groundedNow
        );
    }

    // ------------------------
    // Input callbacks
    // ------------------------
    private void OnFlyStarted(InputAction.CallbackContext ctx) => flyKeyHeld = true;
    private void OnFlyCanceled(InputAction.CallbackContext ctx) => flyKeyHeld = false;

    private void OnBoostStarted(InputAction.CallbackContext ctx) => boostHeld = true;
    private void OnBoostCanceled(InputAction.CallbackContext ctx) => boostHeld = false;

    // ------------------------
    // Inspector settings containers (Serializeable)
    // ------------------------

    [System.Serializable]
    private class FallSettings
    {
        public float maxFallSpeed = 7f;
    }
}
