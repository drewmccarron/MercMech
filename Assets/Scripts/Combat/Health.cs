using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [SerializeField, Tooltip("Current health at runtime (read-only in inspector).")]
    private float currentHealth;

    [SerializeField]
    private bool destroyOnDeath = true;


    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;

    private void Awake()
    {
        // Start full unless you want persistence later.
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        // Optional: for quick testing.
        // Debug.Log($"{name} took {amount} damage. HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f && destroyOnDeath)
        {
            // For a dummy target, destroying is fine.
            Destroy(gameObject);
        }
    }
}
