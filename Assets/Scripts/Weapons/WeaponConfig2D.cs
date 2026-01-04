using UnityEngine;

[CreateAssetMenu(menuName = "MercMech/Combat/Weapon Config 2D", fileName = "WeaponConfig2D")]
public class WeaponConfig2D : ScriptableObject
{
    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 20f;

    // NEW: projectile lifetime belongs to the weapon
    public float projectileLifetimeSeconds = 3f;

    [Header("Stats")]
    public float fireCooldownSeconds = 0.2f;
    public float damagePerHit = 50f;

    [Header("Spawn")]
    public float muzzleForwardOffset = 0.15f;

    public Projectile2D ProjectilePrefabTyped
    {
        get
        {
            if (projectilePrefab == null) return null;

            // Prefer root, but allow child so prefab structure stays flexible
            var p = projectilePrefab.GetComponent<Projectile2D>();
            if (p != null) return p;

            return projectilePrefab.GetComponentInChildren<Projectile2D>();
        }
    }
}
