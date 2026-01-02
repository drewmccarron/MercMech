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
    [Tooltip("Energy regen per second while grounded/walking.")]
    [SerializeField] private float groundRegenRate = 25f;

    [Tooltip("Energy regen per second while falling (airborne, not flying).")]
    [SerializeField] private float fallingRegenRate = 10f;

    [Header("Costs")]
    [Tooltip("Energy drained per second while flying (when FlightMotor IsFlying == true).")]
    [SerializeField] private float flyingEnergyCostRate = 20f;

    [Tooltip("Flat energy cost when starting a quick boost.")]
    [SerializeField] private float quickBoostCost = 25f;

    [Tooltip("Energy drained per second while holding horizontal boost (ground or air).")]
    [SerializeField] private float horizontalBoostEnergyCostRate = 8f;

    [Tooltip("Flat energy cost when starting horizontal boost.")]
    [SerializeField] private float horizontalBoostStartCost = 10f;

    [Tooltip("Flat energy cost when flight begins (prevents holding fly while falling).")]
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

        // Drain for horizontal boost regardless of grounded/airborne
        if (boostHeld)
            AddEnergy(-horizontalBoostEnergyCostRate * dt);

        // Drain for flying
        if (isFlying)
            AddEnergy(-flyingEnergyCostRate * dt);

        // Regen only if not flying and not boosting
        if (!isFlying && !boostHeld)
        {
            float regen = groundedNow ? groundRegenRate : fallingRegenRate;
            AddEnergy(regen * dt);
        }

        EmitIfChanged(force: false);
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

    public bool HasMinimumEnergyForBoost() => currentEnergy > horizontalBoostStartCost;

    public bool HasMinimumEnergyForFlight() => currentEnergy > flightStartCost;

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
