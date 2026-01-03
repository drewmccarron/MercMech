using UnityEngine;
using UnityEngine.UI;

public class UIBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] internal PlayerStats playerStats;
    [SerializeField] internal Image fillImage;
    [SerializeField] internal RectTransform barContainer;

    [Header("Settings")]
    [Tooltip("Width multiplier per max value unit.")]
    [SerializeField] private float widthPerUnit = 3f;

    internal void UpdateBarWidth(float maxResourceValue)
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
        float newWidth = maxResourceValue * widthPerUnit;

        // Update the bar container width while preserving height
        Vector2 size = barContainer.sizeDelta;
        size.x = newWidth;
        barContainer.sizeDelta = size;
    }

    internal void HandleBarChanged(float percentage)
    {
        fillImage.fillAmount = Mathf.Clamp01(percentage);
    }
}