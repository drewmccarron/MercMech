public interface IDamageable
{
    Team Team { get; }
    void TakeDamage(in DamageInfo info);
}
