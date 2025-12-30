using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Projectile2D : MonoBehaviour
{
    [Header("Hit Filtering")]
    [SerializeField]
    private LayerMask hitMask = ~0; // default: everything

    private Rigidbody2D rb;
    private float lifeTimer;
    private Collider2D ownerCollider;

    [SerializeField]
    private float damage;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // If Init isn't called for some reason, fail-safe lifetime so we don't leak objects forever.
        lifeTimer = 3f;
    }

    public void Init(Vector2 velocity, Collider2D ownerToIgnore, float damageAmount, float lifetimeSeconds)
    {
        ownerCollider = ownerToIgnore;
        damage = damageAmount;
        rb.linearVelocity = velocity;
        lifeTimer = Mathf.Max(0.01f, lifetimeSeconds);
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

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
            damageable.TakeDamage(damage);

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

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
            damageable.TakeDamage(damage);

        Destroy(gameObject);
    }
}
