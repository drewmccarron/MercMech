using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStats : MonoBehaviour
{
    [Header("Core Stats")]
    [SerializeField] private Health health;

    // Events that UI / other systems can subscribe to.
    public event Action<float, float> OnHealthChanged; // (current, max)
    public event Action OnDied;

    public Health Health => health;

    public float CurrentHealth => health != null ? health.CurrentHealth : 0f;
    public float MaxHealth => health != null ? health.MaxHealth : 0f;

    private float lastCurrentHealth = -1f;
    private float lastMaxHealth = -1f;

    private void Reset()
    {
        // Auto-wire if possible when added.
        health = GetComponent<Health>();
    }

    private void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();

        // Emit initial values so UI can initialize.
        EmitHealthIfChanged(force: true);
    }

    private void Update()
    {
        // Lightweight polling: avoids tight coupling and still supports any damage source.
        EmitHealthIfChanged(force: false);

        // Death detection (Health may destroy the object; but if you later change that, this still works)
        if (health != null && health.CurrentHealth <= 0f)
        {
            // Fire once.
            if (lastCurrentHealth > 0f)
                OnDied?.Invoke();
        }
    }

    private void EmitHealthIfChanged(bool force)
    {
        if (health == null) return;

        float cur = health.CurrentHealth;
        float max = health.MaxHealth;

        if (force || !Mathf.Approximately(cur, lastCurrentHealth) || !Mathf.Approximately(max, lastMaxHealth))
        {
            lastCurrentHealth = cur;
            lastMaxHealth = max;
            OnHealthChanged?.Invoke(cur, max);
        }
    }
}
