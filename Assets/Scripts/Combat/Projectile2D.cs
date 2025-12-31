using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Projectile2D : MonoBehaviour
{
    [Header("Tuning")]
    [SerializeField] private float damage;
    [SerializeField] private float lifetime;

    [Header("Runtime")]
    [SerializeField] private Team sourceTeam;
    [SerializeField] private GameObject source;

    private float lifeTimer;
    private Rigidbody2D rb;

    public void Init(
        Team team,
        GameObject sourceObject,
        WeaponConfig2D weaponConfig,
        Vector2 direction
    )
    {
        sourceTeam = team;
        source = sourceObject;
        damage = weaponConfig.damagePerHit;
        lifetime = weaponConfig.projectileLifetimeSeconds;

        rb.linearVelocity = direction.normalized * weaponConfig.projectileSpeed;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        lifeTimer = 0f;
    }

    private void Update()
    {
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifetime)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Layer matrix already prevents same-team hurtbox collisions,
        // but keep this so the projectile also ignores same-team IDamageable
        // if someone forgets to layer a hurtbox properly.
        if (!other.TryGetComponent<IDamageable>(out var damageable))
            return;

        if (damageable.Team == sourceTeam)
            return;

        var point = (Vector2)transform.position;
        var normal = Vector2.zero; // triggers don’t provide contact normals

        var info = new DamageInfo(damage, point, normal, source, sourceTeam);
        damageable.TakeDamage(in info);

        Destroy(gameObject);
    }
}
