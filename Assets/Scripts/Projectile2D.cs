using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Projectile2D : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField]
    private float maxLifetime = 3f;

    [Header("Hit Filtering")]
    [SerializeField]
    private LayerMask hitMask = ~0; // default: everything

    private Rigidbody2D rb;
    private float lifeTimer;
    private Collider2D ownerCollider;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        lifeTimer = maxLifetime;
    }

    public void Init(Vector2 velocity, Collider2D ownerToIgnore = null)
    {
        ownerCollider = ownerToIgnore;
        rb.linearVelocity = velocity;
    }


    private void Update()
    {
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        if (other == ownerCollider) return;

        int otherLayerMask = 1 << other.gameObject.layer;
        if ((hitMask.value & otherLayerMask) == 0)
            return;

        Destroy(gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null) return;

        Collider2D other = collision.collider;
        if (other == null) return;
        if (other == ownerCollider) return;

        int otherLayerMask = 1 << other.gameObject.layer;
        if ((hitMask.value & otherLayerMask) == 0)
            return;

        Destroy(gameObject);
    }
}
