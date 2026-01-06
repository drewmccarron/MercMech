using System;
using UnityEngine;

[DisallowMultipleComponent]
public class Health : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [SerializeField, Tooltip("Current health at runtime (read-only in inspector).")]
    private float currentHealth;

    [SerializeField]
    private bool destroyOnDeath = true;

    // Event for reactive systems (UI, PlayerStats, etc.)
    public event Action<float, float> OnHealthChanged; // (current, max)
    public event Action OnDeath; // Fires once when health reaches zero

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;

    private void Awake()
    {
        // Start full unless you want persistence later.
        currentHealth = maxHealth;

        // Emit initial value so listeners can initialize.
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        // Emit change
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Optional: for quick testing.
        // Debug.Log($"{name} took {amount} damage. HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f && destroyOnDeath)
        {
            // Fire death event before destroying
            OnDeath?.Invoke();
            Destroy(gameObject);
        }
    }
}
