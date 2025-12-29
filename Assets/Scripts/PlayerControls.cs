using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControls : MonoBehaviour
{
    private Rigidbody2D rb;
    private Actions controls;
    private Collider2D col;
    private float moveInput;

    [Header("Move")]
    public float walkSpeed = 5f;
    public float boostSpeed = 9f;

    [Header("Jump")]
    public float jumpForce = 10f;
    public float groundCheckDistance = 0.1f;
    public LayerMask groundLayer;

    [Header("Boost")]
    private bool boostHeld;

    [Header("Fly")]
    public float flyAcceleration = 30f;      // upward acceleration while holding Space
    public float maxFlyUpSpeed = 5f;         // cap upward speed
    public float flyGravityScale = 2f;     // gravity while flying
    public float normalGravityScale = 3f;    // gravity normally
    private bool flyHeld;

    void Start()
    {

    }

    void Update()
    {
        moveInput = controls.Player.Walk.ReadValue<float>();
    }

    void Awake()
    {
        controls = new Actions();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.gravityScale = normalGravityScale;
    }


    private void OnEnable()
    {
        controls.Player.Enable();
        controls.Player.Jump.performed += OnJump;

        // Boost
        controls.Player.GroundBoost.started += OnBoostStarted;
        controls.Player.GroundBoost.canceled += OnBoostCanceled;

        // Fly
        controls.Player.Fly.started += OnFlyStarted;
        controls.Player.Fly.canceled += OnFlyCanceled;
    }

    private void OnDisable()
    {
        controls.Player.Jump.performed -= OnJump;

        controls.Player.GroundBoost.started -= OnBoostStarted;
        controls.Player.GroundBoost.canceled -= OnBoostCanceled;

        controls.Player.Fly.started -= OnFlyStarted;
        controls.Player.Fly.canceled -= OnFlyCanceled;

        controls.Player.Disable();
    }

    private void FixedUpdate()
    {
        float speed = boostHeld ? boostSpeed : walkSpeed;
        rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);

        bool grounded = IsGrounded();
        if (flyHeld)
        {
            rb.gravityScale = flyGravityScale;

            rb.AddForce(Vector2.up * flyAcceleration, ForceMode2D.Force);

            // cap upward speed
            if (rb.linearVelocity.y > maxFlyUpSpeed)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, maxFlyUpSpeed);
        }
        else
        {
            rb.gravityScale = normalGravityScale;

            //clear fly state when landing
            if (grounded) flyHeld = false;
        }
    }

    private void OnJump(InputAction.CallbackContext ctx)
    {
        TryJump();
    }

    private void TryJump()
    {
        if (IsGrounded())
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    private bool IsGrounded()
    {
        Vector2 origin = new Vector2(col.bounds.center.x, col.bounds.min.y);
        Vector2 size = new Vector2(col.bounds.size.x * 0.9f, 0.05f);
        float dist = groundCheckDistance;

        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, dist, groundLayer);
        return hit.collider != null;
    }

    private void OnFlyStarted(InputAction.CallbackContext ctx) => flyHeld = true;
    private void OnFlyCanceled(InputAction.CallbackContext ctx) => flyHeld = false;

    private void OnBoostStarted(InputAction.CallbackContext ctx) => boostHeld = true;
    private void OnBoostCanceled(InputAction.CallbackContext ctx) => boostHeld = false;
}
