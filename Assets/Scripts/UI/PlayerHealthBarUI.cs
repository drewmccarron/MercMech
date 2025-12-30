using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private Image fillImage;

    private void OnEnable()
    {
        if (playerStats != null)
            playerStats.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        if (playerStats != null)
            playerStats.OnHealthChanged -= HandleHealthChanged;
    }

    private void Start()
    {
        if (playerStats != null)
            HandleHealthChanged(playerStats.CurrentHealth, playerStats.MaxHealth);
    }

    private void HandleHealthChanged(float current, float max)
    {
        float pct = max > 0f ? current / max : 0f;
        fillImage.fillAmount = Mathf.Clamp01(pct);
    }
}