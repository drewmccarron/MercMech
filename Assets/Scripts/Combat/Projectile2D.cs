using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Projectile2D : MonoBehaviour
{
    [Header("Runtime")]
    [SerializeField] private Team sourceTeam;
    [SerializeField] private GameObject source;

    [Header("World Collision")]
    [SerializeField] private LayerMask worldMask;
    [SerializeField] private bool destroyOnWorldHit = true;

    [Header("Interactable")]
    [SerializeField] private bool isInterceptable;

    private float lifeTimer;
    private Rigidbody2D rb;
    private WeaponConfig2D weaponConfig;

    public void Init(
        Team team,
        GameObject sourceObject,
        WeaponConfig2D weaponConfig2D,
        Vector2 direction
    )
    {
        sourceTeam = team;
        source = sourceObject;
        weaponConfig = weaponConfig2D;

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
        if (lifeTimer >= weaponConfig.projectileLifetimeSeconds)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<Projectile2D>(out var otherProj))
        {
            InterceptProjectile(otherProj);
        }

        if (other.TryGetComponent<IDamageable>(out var damageable))
        {
            if (damageable.Team == sourceTeam)
                return;

            var point = (Vector2)transform.position;
            var normal = Vector2.zero;

            var info = new DamageInfo(weaponConfig.damagePerHit, point, normal, source, sourceTeam);
            damageable.TakeDamage(in info);

            Destroy(gameObject);
            return;
        }

        // World hit
        if (destroyOnWorldHit && ((worldMask.value & (1 << other.gameObject.layer)) != 0))
        {
            Destroy(gameObject);
        }
    }

    public void InterceptProjectile(Projectile2D otherProj)
    {
        if (!isInterceptable && !otherProj.isInterceptable)
            return;
        Destroy(otherProj.gameObject);
        Destroy(gameObject);
    }
}
