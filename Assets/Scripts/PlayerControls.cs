using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControls : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private Actions controls;


    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void Awake()
    {
        controls = new Actions();
    }


    private void OnEnable()
    {
        controls.Player.Left.performed += moveLeft;
        controls.Player.Right.performed += moveRight;
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Left.performed -= moveLeft;
        controls.Player.Right.performed -= moveRight;
        controls.Player.Disable();
    }

    private void moveLeft(InputAction.CallbackContext context)
    {
        Debug.Log("Move Left");
    }

    private void moveRight(InputAction.CallbackContext context)
    {
        Debug.Log("Move Right");
    }
}
