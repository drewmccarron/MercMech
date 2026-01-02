using System;
using UnityEngine;

[DisallowMultipleComponent]
public class EnergyPool : MonoBehaviour
{
  [Header("Energy")]
  [SerializeField] private float maxEnergy = 100f;

  [SerializeField, Tooltip("Current energy at runtime (read-only in inspector).")]
  private float currentEnergy;

  [Header("Regen Rates (per second)")]
  [Tooltip("Energy regen per second while grounded/walking (not boosting).")]
  [SerializeField] private float groundRegenRate = 30f;

  [Tooltip("Energy regen per second while grounded AND boosting (slower than walking).")]
  [SerializeField] private float groundBoostRegenRate = 15f;

  [Tooltip("Energy regen per second while falling (airborne, not flying, not boosting).")]
  [SerializeField] private float fallingRegenRate = 20f;

  [Tooltip("Energy regen per second while falling AND boosting (slower than normal falling).")]
  [SerializeField] private float fallingBoostRegenRate = 15f;

  [Header("Costs")]
  [Tooltip("Energy drained per second while flying (when FlightMotor IsFlying == true).")]
  [SerializeField] private float flyingEnergyCostRate = 20f;

  [Tooltip("Flat energy cost when starting a quick boost.")]
  [SerializeField] private float quickBoostCost = 25f;

  [Tooltip("Flat energy cost when starting horizontal boost.")]
  [SerializeField] private float horizontalBoostStartCost = 10f;

  [Tooltip("Minimum energy required to START flight. Flight can continue draining below this threshold.")]
  [SerializeField] private float flightStartCost = 8f;

  [Header("Rules")]
  [Tooltip("If true, regen does not occur while quick boosting.")]
  [SerializeField] private bool freezeRegenDuringQuickBoost = true;

  // Events for UI / other systems
  public event Action<float, float> OnEnergyChanged; // (current, max)

  public float MaxEnergy => maxEnergy;
  public float CurrentEnergy => currentEnergy;

  public bool HasEnergy => currentEnergy > 0.0001f;

  private float lastCurrent = -1f;
  private float lastMax = -1f;

  private void Awake()
  {
    currentEnergy = Mathf.Clamp(currentEnergy <= 0f ? maxEnergy : currentEnergy, 0f, maxEnergy);
    EmitIfChanged(force: true);
  }

  // Called by PlayerControls (or another orchestrator) once per frame.
  public void TickEnergy(bool groundedNow, bool boostHeld, bool isFlying, bool isQuickBoosting, float dt)
  {
    if (dt <= 0f) return;

    // Freeze regen during QB if enabled (spec requirement).
    if (freezeRegenDuringQuickBoost && isQuickBoosting)
    {
      EmitIfChanged(force: false);
      return;
    }

    // Drain for flying
    if (isFlying)
    {
      AddEnergy(-flyingEnergyCostRate * dt);
    }
    else
    {
      // Not flying: apply regen (reduced if boosting)
      float regen = GetRegenRate(groundedNow, boostHeld);
      AddEnergy(regen * dt);
    }

    EmitIfChanged(force: false);
  }

  // Determine regen rate based on grounded state and boost state
  private float GetRegenRate(bool groundedNow, bool boostHeld)
  {
    if (groundedNow)
    {
      // Grounded: boost reduces regen but doesn't drain
      return boostHeld ? groundBoostRegenRate : groundRegenRate;
    }
    else
    {
      // Airborne (falling): boost reduces regen but doesn't drain
      return boostHeld ? fallingBoostRegenRate : fallingRegenRate;
    }
  }

  // Called when attempting to start QuickBoost.
  // Returns true if energy was spent successfully.
  public bool TrySpendQuickBoost()
  {
    if (currentEnergy < quickBoostCost)
      return false;

    currentEnergy -= quickBoostCost;
    EmitIfChanged(force: false);
    return true;
  }

  // Check if player has minimum energy to START horizontal boost
  public bool CanStartBoost() => currentEnergy >= horizontalBoostStartCost;

  // Check if player has minimum energy to START flight
  public bool CanStartFlight() => currentEnergy >= flightStartCost;

  // Utility for future systems (weapons, etc.)
  public bool TrySpend(float amount)
  {
    if (amount <= 0f) return true;
    if (currentEnergy < amount) return false;

    currentEnergy -= amount;
    EmitIfChanged(force: false);
    return true;
  }

  public void AddEnergy(float amount)
  {
    if (Mathf.Approximately(amount, 0f)) return;
    currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, maxEnergy);
  }

  private void EmitIfChanged(bool force)
  {
    if (force || !Mathf.Approximately(currentEnergy, lastCurrent) || !Mathf.Approximately(maxEnergy, lastMax))
    {
      lastCurrent = currentEnergy;
      lastMax = maxEnergy;
      OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }
  }
}
