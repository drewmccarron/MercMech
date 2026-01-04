using UnityEngine;

public class MovementDebugSamples
{
    public const int SampleCount = 60;

    public readonly float[] speedMag = new float[SampleCount];
    public readonly float[] speedX = new float[SampleCount];
    public readonly float[] speedY = new float[SampleCount];
    public readonly float[] accel = new float[SampleCount];

    int writeIndex;
    Vector2 lastVelocity;

    public int WriteIndex => writeIndex;

    public void Sample(Rigidbody2D rb, float dt)
    {
        Vector2 v = rb.linearVelocity;

        speedMag[writeIndex] = v.magnitude;
        speedX[writeIndex] = v.x;
        speedY[writeIndex] = v.y;
        accel[writeIndex] = (v - lastVelocity).magnitude / dt;

        lastVelocity = v;
        writeIndex = (writeIndex + 1) % SampleCount;
    }
}
