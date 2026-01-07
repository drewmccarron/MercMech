using UnityEngine;
using UnityEngine.InputSystem; // <-- NEW input system

public class AimBiasedCameraTarget2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private PlayerControls playerControls;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Camera worldCamera;

    [Header("Bias")]
    [Range(0f, 1f)]
    [SerializeField] private float xBias = 0.15f;
    [Range(0f, 1f)]
    [SerializeField] private float yBias = 0.45f;

    [SerializeField] private float maxOffset = 6f; // prevents crazy offsets
    [SerializeField] private float followSmooth = 1.5f; // higher = snappier

    void Reset()
    {
        playerTransform = transform;
    }

    void Awake()
    {
        if (playerTransform == null) playerTransform = transform;
        if (worldCamera == null) worldCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (playerTransform == null || cameraTarget == null || playerControls == null)
            return;

        Vector3 playerPosition = playerTransform.position;
        Vector2 aim = playerControls.AimWorldPosition;

        float desiredX = Mathf.Clamp(aim.x - playerPosition.x, -maxOffset, maxOffset) * xBias;
        float desiredY = Mathf.Clamp(aim.y - playerPosition.y, -maxOffset, maxOffset) * yBias;

        Vector3 desired = new Vector3(playerPosition.x + desiredX, playerPosition.y + desiredY, playerPosition.z);

        float t = 1f - Mathf.Exp(-followSmooth * Time.unscaledDeltaTime);
        cameraTarget.position = Vector3.Lerp(cameraTarget.position, desired, t);
    }
}
