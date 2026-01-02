using System.Text;
using TMPro;
using UnityEngine;

public class DebugHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text text;

    [Header("References")]
    [SerializeField] private PlayerControls player;   // drag your Player object here

    [Header("Formatting")]
    [SerializeField] private int decimals = 2;

    private readonly StringBuilder sb = new StringBuilder(512);

    private void Awake()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (player == null) player = FindObjectOfType<PlayerControls>();

        // Hide by default if debug is off
        SetVisible(DebugSettings.Enabled);
    }

    private void Update()
    {
        bool enabled = DebugSettings.Enabled;

        // Show/hide the HUD with debug
        if (gameObject.activeSelf != enabled)
            SetVisible(enabled);

        if (!enabled) return;

        if (player == null)
        {
            text.text = "DebugHUD: No PlayerControls found.";
            return;
        }

        Render();
    }

    private void SetVisible(bool visible)
    {
        // Use active state so it doesn't waste layout + updates
        gameObject.SetActive(visible);
    }

    private void Render()
    {
        sb.Clear();

        // Try to read what we can without forcing you to refactor.
        // If a field is private in PlayerControls, expose it with a small public getter.
        var rb = player.Rigidbody; // we'll add this property (see below)
        Vector2 v = rb != null ? rb.linearVelocity : Vector2.zero;

        sb.AppendLine("<b>DEBUG</b>");
        sb.Append("FPS: ").Append(Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime))).AppendLine();

        sb.AppendLine();

        sb.Append("<b>Movement</b>\n");
        sb.Append("Grounded: ").Append(player.IsGrounded ? "YES" : "no").AppendLine();
        sb.Append("Facing: ").Append(player.FacingDirection).AppendLine();
        sb.Append("v: ").Append(Format(v.magnitude)).Append("  (")
          .Append(Format(v.x)).Append(", ").Append(Format(v.y)).Append(")\n");

        sb.Append("Flying: ").Append(player.IsFlying ? "YES" : "no").AppendLine();
        sb.Append("QuickBoost: ").Append(player.IsQuickBoosting ? "YES" : "no").AppendLine();
        sb.Append("BoostHeld: ").Append(player.BoostHeld ? "YES" : "no").AppendLine();

        sb.AppendLine();

        sb.Append("<b>Energy</b>\n");
        sb.Append("Energy: ").Append(Format(player.EnergyCurrent)).Append(" / ").Append(Format(player.EnergyMax)).AppendLine();

        text.text = sb.ToString();
    }

    private string Format(float f)
    {
        return f.ToString("F" + decimals);
    }
}
