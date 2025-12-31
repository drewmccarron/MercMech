using UnityEngine;

[DisallowMultipleComponent]
public class DamageReceiver : MonoBehaviour, IDamageable
{
    [SerializeField] private Combatant combatant;

    private Health health;
    // Later: private EnemyStats enemyStats;

    public Team Team => combatant != null ? combatant.Team : Team.Enemy;

    private void Awake()
    {
        if (combatant == null)
            combatant = GetComponentInParent<Combatant>();

        health = GetComponentInParent<Health>();
        // enemyStats = GetComponentInParent<EnemyStats>();
    }

    public void TakeDamage(in DamageInfo info)
    {
        // Friendly-fire guard (extra safety even though layer matrix handles it)
        if (info.SourceTeam == Team)
            return;

        if (health != null)
        {
            // CHANGE THIS LINE to your real API if needed:
            health.TakeDamage(info.Amount);
            return;
        }

        // if (enemyStats != null) { enemyStats.ApplyDamage(info.Amount); return; }
    }
}
