using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerControls))]
public class PlayerShooting : MonoBehaviour
{
    // Add near the top of the class:
    [Header("Weapon Config")]
    [SerializeField]
    private WeaponConfig2D weaponConfig;

    [Header("Settings")]
    [SerializeField]
    private bool holdToFire = true;

    private PlayerControls player;
    private WeaponMotor2D weaponMotor;
    private Collider2D playerCollider;

    private void Awake()
    {
        player = GetComponent<PlayerControls>();
        weaponMotor = new WeaponMotor2D();
        playerCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        weaponMotor.Tick(Time.deltaTime);

        bool fireHeld = player.FireHeld;
        bool firePressed = player.FirePressedThisFrame;

        if (holdToFire)
        {
            if (!fireHeld) return;
            TryFire();
        }
        else
        {
            if (!firePressed) return;
            TryFire();
        }
    }

    private void TryFire()
    {
        if (weaponConfig == null)
            return;
        Projectile2D prefab = weaponConfig.projectilePrefab;
        if (prefab == null)
            return;

        float cooldown = weaponConfig.fireCooldownSeconds;
        float speed = weaponConfig.projectileSpeed;
        float dmg = weaponConfig.damagePerHit;
        float muzzleOffset = weaponConfig.muzzleForwardOffset;

        if (!weaponMotor.TryConsumeFire(cooldown))
            return;

        Vector2 origin = player.AimOriginWorld;
        Vector2 dir = player.AimDirection;

        Vector2 spawnPos = origin + dir * muzzleOffset;

        Projectile2D proj = Instantiate(prefab, spawnPos, Quaternion.identity);
        proj.Init(dir * speed, playerCollider, dmg);
    }
}
