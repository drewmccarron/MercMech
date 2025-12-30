using UnityEngine;

[RequireComponent(typeof(PlayerControls))]
public class AimVisualizer2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform aimPointVisual;   // assign a small sprite/marker transform

    [Header("Ray Settings")]
    [SerializeField] private float rayLength = 25f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRay = false;

    private PlayerControls player;

    private void Awake()
    {
        player = GetComponent<PlayerControls>();
    }

    private void LateUpdate()
    {
        if (player == null) return;

        Vector2 origin = player.AimOriginWorld;
        Vector2 aimPoint = player.AimWorldPosition;
        Vector2 dir = player.AimDirection;

        if (aimPointVisual != null)
            aimPointVisual.position = new Vector3(aimPoint.x, aimPoint.y, aimPointVisual.position.z);

        Vector2 end = origin + dir * rayLength;

        if (drawDebugRay)
        {
            Debug.DrawLine(origin, end, Color.white);
        }
    }
}
