public class PlayerEnergyBar : UIBar
{
    private void OnEnable()
    {
        if (playerStats != null)
            playerStats.OnEnergyChanged += HandleBarChanged;
    }

    private void OnDisable()
    {
        if (playerStats != null)
            playerStats.OnEnergyChanged -= HandleBarChanged;
    }

    private void Start()
    {
        if (playerStats != null)
        {
            UpdateBarWidth(playerStats.MaxEnergy);
            HandleBarChanged(1);
        }
    }
}
