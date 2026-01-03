using UnityEngine;
using UnityEngine.UI;

public class PlayerEnergyBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private Image fillImage;
    [SerializeField] private RectTransform barContainer;

    [Header("Settings")]
    [Tooltip("Width multiplier per max energy unit.\nDefault: 3 pixels per energy point")]
    [SerializeField] private float widthPerEnergyUnit = 3f;

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
        {
            HandleEnergyChanged(playerStats.CurrentEnergy, playerStats.MaxEnergy);
            UpdateBarWidth(playerStats.MaxEnergy);
        }
    }

    private void HandleEnergyChanged(float current, float max)
    {
        float pct = max > 0f ? current / max : 0f;
        fillImage.fillAmount = Mathf.Clamp01(pct);
    }

    private void UpdateBarWidth(float maxEnergy)
    {
        if (barContainer == null)
        {
            // Try to find the RectTransform if not assigned
            barContainer = fillImage != null ? fillImage.rectTransform.parent as RectTransform : null;

            if (barContainer == null)
            {
                Debug.LogWarning("[PlayerEnergyBarUI] Bar container RectTransform not found. Assign it in the inspector.");
                return;
            }
        }

        // Calculate new width: 3 pixels per energy point
        float newWidth = maxEnergy * widthPerEnergyUnit;

        // Update the bar container width while preserving height
        Vector2 size = barContainer.sizeDelta;
        size.x = newWidth;
        barContainer.sizeDelta = size;
    }
}
