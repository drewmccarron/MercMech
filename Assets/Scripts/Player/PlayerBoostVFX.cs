using UnityEngine;

public class PlayerBoostVFX : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerControls player;

    [Header("Particle Systems")]
    [SerializeField] private ParticleSystem quickBoostRed;
    [SerializeField] private ParticleSystem flightBlue;
    [SerializeField] private ParticleSystem boostGreen;

    [Header("Emission (particles/sec)")]
    [SerializeField] private float qbEmissionMax = 120f;
    [SerializeField] private float flightEmissionMax = 70f;
    [SerializeField] private float boostEmissionMax = 60f;

    [Header("Smoothing")]
    [Tooltip("How quickly intensities rise toward their targets.")]
    [SerializeField] private float intensityRiseSpeed = 18f;

    [Tooltip("How quickly intensities fall toward their targets.")]
    [SerializeField] private float intensityFallSpeed = 22f;

    [Header("Speed scaling (hybrid boost)")]
    [Tooltip("Speed at/above which speed01 becomes 1.0 for boost scaling.")]
    [SerializeField] private float speedForMax = 12f;

    [Tooltip("Minimum visible boost intensity when boost is active, even at low speed.")]
    [Range(0f, 1f)]
    [SerializeField] private float minVisualBoost = 0.2f;

    [Header("Mode intensities (optional)")]
    [Tooltip("If true, QB/Flight emissions also scale with speed01 (in addition to their own intent).")]
    [SerializeField] private bool scaleQBAndFlightBySpeed = true;

    private float qb01, flight01, boost01;

    private void Reset()
    {
        player = GetComponent<PlayerControls>();
    }

    private void LateUpdate()
    {
        if (player == null)
            return;

        // --- Compute speed01 (outcome-based scaling) ---
        float speed01 = 1f;
        if (player.Rigidbody != null)
        {
            float speed = player.Rigidbody.linearVelocity.magnitude;
            speed01 = Mathf.Clamp01(speedForMax <= 0.0001f ? 1f : (speed / speedForMax));
        }

        // --- Raw intents (0..1) ---
        float targetQB = player.IsQuickBoosting ? player.QuickBoostStrength01 : 0f;

        // If you later expose a true throttle (recommended), replace this:
        float targetFlight = player.FlyThrottle01;  // 0..1

        // Hybrid boost: when boost is active, scale by speed (honest),
        // but ensure minimum visible thrust so the player still "feels" boost visually at low speed.
        float targetBoost = 0f;
        if (player.IsBoosting)
            targetBoost = Mathf.Max(speed01, minVisualBoost);

        // --- Smooth intensities (so emission doesn't pop) ---
        qb01 = Move01(qb01, targetQB, Time.unscaledDeltaTime);
        flight01 = Move01(flight01, targetFlight, Time.unscaledDeltaTime);
        boost01 = Move01(boost01, targetBoost, Time.unscaledDeltaTime);

        // --- Apply emissions ---
        float qbScale = scaleQBAndFlightBySpeed ? speed01 : 1f;
        float flightScale = scaleQBAndFlightBySpeed ? speed01 : 1f;

        ApplyEmission(quickBoostRed, qbEmissionMax * qb01 * qbScale);
        ApplyEmission(flightBlue, flightEmissionMax * flight01 * flightScale);
        ApplyEmission(boostGreen, boostEmissionMax * boost01);
    }

    private float Move01(float current, float target, float dt)
    {
        float speed = target > current ? intensityRiseSpeed : intensityFallSpeed;
        return Mathf.MoveTowards(current, target, speed * dt);
    }

    private void ApplyEmission(ParticleSystem ps, float rate)
    {
        if (ps == null)
            return;

        var emission = ps.emission;
        emission.rateOverTime = rate;

        if (rate > 0.01f)
        {
            if (!ps.isPlaying)
                ps.Play();
        }
        else
        {
            // Stop emitting but let existing particles finish
            if (ps.isPlaying)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
