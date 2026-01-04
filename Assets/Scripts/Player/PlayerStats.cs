using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStats : MonoBehaviour
{
  [Header("Core Stats")]
  [SerializeField] private Health health;
  [SerializeField] private EnergyPool energy;

  // Events that UI / other systems can subscribe to.
  public event Action<float> OnEnergyChanged;

  public EnergyPool Energy => energy;

  public float CurrentEnergy => energy != null ? energy.CurrentEnergy : 0f;
  public float MaxEnergy => energy != null ? energy.MaxEnergy : 0f;

  // Events that UI / other systems can subscribe to.
  public event Action<float> OnHealthChanged;
  public event Action OnDied;

  public Health Health => health;

  public float CurrentHealth => health != null ? health.CurrentHealth : 0f;
  public float MaxHealth => health != null ? health.MaxHealth : 0f;

  private float lastCurrentHealth = -1f;

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
    {
      energy.OnEnergyChanged += HandleEnergyChanged;
      HandleEnergyChanged(energy.CurrentEnergy, energy.MaxEnergy);
    }  

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
    float percentage = max > 0f ? cur / max : 0f;
    OnHealthChanged?.Invoke(percentage);

    // Death detection (fire once when health crosses to zero)
    if (cur <= 0f && lastCurrentHealth > 0f)
      OnDied?.Invoke();

    lastCurrentHealth = cur;
  }

  private void HandleEnergyChanged(float cur, float max)
  {
    float percentage = max > 0f ? cur / max : 0f;
    OnEnergyChanged?.Invoke(percentage);
  }
}
