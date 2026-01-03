public class PlayerHealthBar : UIBar
{
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
        {
            UpdateBarWidth(playerStats.MaxHealth);
            HandleBarChanged(1);
        }
    }

    private void HandleHealthChanged(float pct)
    {
        HandleBarChanged(pct);
    }
}