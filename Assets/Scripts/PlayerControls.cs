using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControls : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private Rigidbody2D rb;
    private Actions controls;
    private float moveInput;
    public float speed = 5f;

    public float jumpForce = 10f;
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.1f;
    private Collider2D col;

    void Start()
    {

    }

    // Update is called once per frame
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
    }

    private void OnDisable()
    {
        controls.Player.Jump.performed -= OnJump;
        controls.Player.Disable();
    }

    private void FixedUpdate()
    {
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
}
