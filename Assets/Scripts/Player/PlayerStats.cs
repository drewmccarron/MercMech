using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStats : MonoBehaviour
{
  [Header("Core Stats")]
  [SerializeField] private Health health;
  [SerializeField] private EnergyPool energy;

  // Events that UI / other systems can subscribe to.
  public event Action<float, float> OnEnergyChanged; // (current, max)

  public EnergyPool Energy => energy;

  public float CurrentEnergy => energy != null ? energy.CurrentEnergy : 0f;
  public float MaxEnergy => energy != null ? energy.MaxEnergy : 0f;

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
    energy = GetComponent<EnergyPool>();
  }

  private void Awake()
  {
    if (health == null)
      health = GetComponent<Health>();

    if (energy == null)
      energy = GetComponent<EnergyPool>();

    if (energy != null)
      energy.OnEnergyChanged += HandleEnergyChanged;

    // Subscribe to health changes (reactive instead of polling).
    if (health != null)
    {
      health.OnHealthChanged += HandleHealthChanged;
      // Initialize cached values and emit to listeners.
      HandleHealthChanged(health.CurrentHealth, health.MaxHealth);
    }
  }

  private void OnDestroy()
  {
    if (energy != null)
      energy.OnEnergyChanged -= HandleEnergyChanged;

    if (health != null)
      health.OnHealthChanged -= HandleHealthChanged;
  }

  private void HandleHealthChanged(float cur, float max)
  {
    OnHealthChanged?.Invoke(cur, max);

    // Death detection (fire once when health crosses to zero)
    if (cur <= 0f && lastCurrentHealth > 0f)
      OnDied?.Invoke();

    lastCurrentHealth = cur;
    lastMaxHealth = max;
  }

  private void HandleEnergyChanged(float current, float max)
  {
    OnEnergyChanged?.Invoke(current, max);
  }
}
