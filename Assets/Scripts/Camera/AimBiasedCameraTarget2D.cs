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
    [SerializeField] private float aimBias = 0.5f; // 0=player, 1=aim point

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

        Vector2 aimWorld = playerControls.AimWorldPosition;

        Vector3 p = playerTransform.position;
        Vector3 towardAim = (Vector3)aimWorld - p;

        Vector3 offset = Vector3.ClampMagnitude(towardAim, maxOffset);
        Vector3 desired = p + offset * aimBias;

        float t = 1f - Mathf.Exp(-followSmooth * Time.unscaledDeltaTime);
        cameraTarget.position = Vector3.Lerp(cameraTarget.position, desired, t);
    }
}
