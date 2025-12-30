using UnityEngine;

public class WeaponMotor2D
{
    private float cooldownTimer;

    public void Tick(float dt)
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= dt;
    }

    public bool TryConsumeFire(float fireCooldownSeconds)
    {
        if (fireCooldownSeconds < 0f)
            fireCooldownSeconds = 0f;

        if (cooldownTimer > 0f)
            return false;

        cooldownTimer = fireCooldownSeconds;
        return true;
    }
}
