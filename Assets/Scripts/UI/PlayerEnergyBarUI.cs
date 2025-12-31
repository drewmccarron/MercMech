using UnityEngine;
using UnityEngine.UI;

public class PlayerEnergyBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private Image fillImage;

    private void OnEnable()
    {
        if (playerStats != null)
            playerStats.OnEnergyChanged += HandleEnergyChanged;
    }

    private void OnDisable()
    {
        if (playerStats != null)
            playerStats.OnEnergyChanged -= HandleEnergyChanged;
    }

    private void Start()
    {
        if (playerStats != null)
            HandleEnergyChanged(playerStats.CurrentEnergy, playerStats.MaxEnergy);
    }

    private void HandleEnergyChanged(float current, float max)
    {
        float pct = max > 0f ? current / max : 0f;
        fillImage.fillAmount = Mathf.Clamp01(pct);
    }
}
