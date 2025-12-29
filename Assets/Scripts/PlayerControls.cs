using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControls : MonoBehaviour
{
    private Rigidbody2D rb;
    private Actions controls;
    private Collider2D col;
    private float moveInput;

    public float walkSpeed = 5f;
    public float boostSpeed = 9f;
    public float jumpForce = 10f;
    public float groundCheckDistance = 0.1f;
    public LayerMask groundLayer;

    private bool boostHeld;
    private float boostAmount;

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
    }


    private void OnEnable()
    {
        controls.Player.Enable();
        controls.Player.Jump.performed += OnJump;

        // Boost held state
        controls.Player.GroundBoost.started += OnBoostStarted;
        controls.Player.GroundBoost.canceled += OnBoostCanceled;
    }

    private void OnDisable()
    {
        controls.Player.Jump.performed -= OnJump;

        controls.Player.GroundBoost.started -= OnBoostStarted;
        controls.Player.GroundBoost.canceled -= OnBoostCanceled;

        controls.Player.Disable();
    }

    private void FixedUpdate()
    {
        float speed = boostHeld ? boostSpeed : walkSpeed;
        rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);
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

    private void OnBoostStarted(InputAction.CallbackContext ctx) => boostHeld = true;
    private void OnBoostCanceled(InputAction.CallbackContext ctx) => boostHeld = false;
}
