using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControls : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private Rigidbody2D rb;
    private Actions controls;
    private float moveInput;
    public float speed = 5f;

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
    }


    private void OnEnable()
    {
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);
    }
}
