using UnityEngine;

public class PlayerBoostVFX : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerControls player;

    [Header("Particle Systems")]
    [SerializeField] private ParticleSystem quickBoostRed;
    [SerializeField] private ParticleSystem flightBlue;
    [SerializeField] private ParticleSystem boostGreen;

    [Header("Emitters (Transforms that get rotated)")]
    [SerializeField] private Transform quickBoostEmitter;
    [SerializeField] private Transform flightEmitter;
    [SerializeField] private Transform boostEmitter;

    [Header("Emission (particles/sec)")]
    [SerializeField] private float qbEmissionMax = 2000f;
    [SerializeField] private float flightEmissionMax = 1000f;
    [SerializeField] private float boostEmissionMax = 1000f;

    [Header("Smoothing")]
    [SerializeField] private float intensityRiseSpeed = 18f;
    [SerializeField] private float intensityFallSpeed = 22f;

    [Header("Speed scaling (hybrid boost)")]
    [SerializeField] private float speedForMax = 12f;
    [Range(0f, 1f)]
    [SerializeField] private float minVisualBoost = 0.2f;

    [Header("Orientation")]
    [Tooltip("If your thruster art points RIGHT by default, leave this 0. If it points LEFT, set 180.")]
    [SerializeField] private float emitterForwardAngleOffset = 180f;

    private float qb01, flight01, boost01;

    private void Reset()
    {
        player = GetComponent<PlayerControls>();
    }

    private void LateUpdate()
    {
        if (player == null)
            return;

        // --- Compute speed01 ---
        float speed01 = 1f;
        Vector2 v = Vector2.zero;

        if (player.Rigidbody != null)
        {
            v = player.Rigidbody.linearVelocity;
            float speed = v.magnitude;
            speed01 = Mathf.Clamp01(speedForMax <= 0.0001f ? 1f : (speed / speedForMax));
        }

        // --- Intensity targets ---
        float targetQB = player.IsQuickBoosting ? player.QuickBoostStrength01 : 0f;
        float targetFlight = player.FlyThrottle01; // already 0 when not flying :contentReference[oaicite:3]{index=3}

        float targetBoost = 0f;
        if (player.IsBoosting)
            targetBoost = Mathf.Max(speed01, minVisualBoost);

        qb01 = Move01(qb01, targetQB, Time.unscaledDeltaTime);
        flight01 = Move01(flight01, targetFlight, Time.unscaledDeltaTime);
        boost01 = Move01(boost01, targetBoost, Time.unscaledDeltaTime);

        ApplyEmission(quickBoostRed, qbEmissionMax * qb01);
        ApplyEmission(flightBlue, flightEmissionMax * flight01);
        ApplyEmission(boostGreen, boostEmissionMax * boost01);

        SetEmitterRotation(quickBoostEmitter, new Vector2(-player.FacingDirection, 0f), emitterForwardAngleOffset);
        SetEmitterRotation(boostEmitter, new Vector2(-player.FacingDirection, 0f), emitterForwardAngleOffset);
        SetEmitterRotation(flightEmitter, Vector2.down, emitterForwardAngleOffset);
    }

    private void SetEmitterRotation(Transform emitter, Vector2 dir, float angleOffsetDeg)
    {
        if (emitter == null) return;
        if (dir.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + angleOffsetDeg;
        emitter.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private float Move01(float current, float target, float dt)
    {
        float speed = target > current ? intensityRiseSpeed : intensityFallSpeed;
        return Mathf.MoveTowards(current, target, speed * dt);
    }

    private void ApplyEmission(ParticleSystem ps, float rate)
    {
        if (ps == null) return;

        var emission = ps.emission;
        emission.rateOverTime = rate;

        if (rate > 0.01f)
        {
            if (!ps.isPlaying) ps.Play();
        }
        else
        {
            if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
