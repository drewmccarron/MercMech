using UnityEngine;

public class AutoDisableVFX : MonoBehaviour
{
    [SerializeField] private float extraSeconds = 0.2f;

    private ParticleSystem[] systems;

    private void Awake()
    {
        systems = GetComponentsInChildren<ParticleSystem>(true);
    }

    private void OnEnable()
    {
        float max = 0f;
        foreach (var ps in systems)
        {
            var main = ps.main;
            float life = main.startLifetime.constantMax;
            max = Mathf.Max(max, main.duration + life);
        }
        Invoke(nameof(DisableSelf), max + extraSeconds);
    }

    private void DisableSelf()
    {
        Destroy(gameObject);
    }
}
