using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerControls))]
public class PlayerShooting : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField]
    private Projectile2D projectilePrefab;
    [SerializeField]
    private float projectileSpeed = 20f;

    [Header("Fire")]
    [SerializeField]
    private float fireCooldownSeconds = 0.2f; // adjustable: lower = faster
    [SerializeField]
    private bool holdToFire = true;

    [Header("Spawn")]
    [SerializeField]
    private float muzzleForwardOffset = 0.15f; // small offset along aim dir to avoid immediate overlap

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
        if (projectilePrefab == null)
            return;

        if (!weaponMotor.TryConsumeFire(fireCooldownSeconds))
            return;

        Vector2 origin = player.AimOriginWorld;
        Vector2 dir = player.AimDirection;

        Vector2 spawnPos = origin + dir * muzzleForwardOffset;

        Projectile2D proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        proj.Init(dir * projectileSpeed, playerCollider);
    }
}
