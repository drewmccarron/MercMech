using UnityEngine;

[CreateAssetMenu(menuName = "MercMech/Combat/Weapon Config 2D", fileName = "WeaponConfig2D")]
public class WeaponConfig2D : ScriptableObject
{
    [Header("Projectile")]
    public Projectile2D projectilePrefab;
    public float projectileSpeed = 20f;

    // NEW: projectile lifetime belongs to the weapon
    public float projectileLifetimeSeconds = 3f;

    [Header("Stats")]
    public float fireCooldownSeconds = 0.2f;
    public float damagePerHit = 50f;

    [Header("Spawn")]
    public float muzzleForwardOffset = 0.15f;
}
