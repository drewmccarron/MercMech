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
        float x = controls.Player.Walk.ReadValue<float>();
    }

    void Awake()
    {
        controls = new Actions();
    }


    private void OnEnable()
    {

    }

    private void OnDisable()
    {

    }
}
