using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyAutoShooter2D : MonoBehaviour
{
    [Header("Target")]
    [SerializeField]
    private GameObject target; // assign Player transform

    [Header("Weapon Config")]
    [SerializeField]
    private WeaponConfig2D weaponConfig;

    [Header("Aim Origin")]
    [SerializeField, Tooltip("If null, uses this transform.")]
    private Transform muzzleOrigin;

    private readonly AimMotor2D aimMotor = new AimMotor2D();
    private readonly WeaponMotor2D weaponMotor = new WeaponMotor2D();
    private Transform targetTf;


    private void Awake()
    {
        if (muzzleOrigin == null)
            muzzleOrigin = transform;

        target = GameObject.FindGameObjectWithTag("Player");
        if (target != null)
            targetTf = target.transform;
    }

    private void Update()
    {
        // Tick cooldown every frame.
        weaponMotor.Tick(Time.deltaTime);

        // Quick guards: config, prefab, target required.
        if (weaponConfig == null || weaponConfig.ProjectilePrefabTyped == null || target == null)
            return;

        // If not ready to fire, skip aim/calculation.
        if (!weaponMotor.TryConsumeFire(weaponConfig.fireCooldownSeconds))
            return;

        Vector2 origin = muzzleOrigin.position;
        Vector2 targetWorld = targetTf.position;

        // Aim at player using existing AimMotor.
        aimMotor.UpdateAimWorld(origin, targetWorld);
        Vector2 dir = aimMotor.AimDirection;

        Vector2 spawnPos = origin + dir * weaponConfig.muzzleForwardOffset;

        Projectile2D proj = Instantiate(weaponConfig.ProjectilePrefabTyped, spawnPos, Quaternion.identity);
        proj.Init(Team.Enemy, gameObject, weaponConfig, dir);
    }
}
