using UnityEngine;

public static class DebugGL2D
{
    public static void Line(Vector2 a, Vector2 b)
    {
        GL.Vertex3(a.x, a.y, 0f);
        GL.Vertex3(b.x, b.y, 0f);
    }

    public static void WireBox(Vector2 center, Vector2 size, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector2 ex = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (size.x * 0.5f);
        Vector2 ey = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * (size.y * 0.5f);

        Vector2 p0 = center - ex - ey;
        Vector2 p1 = center + ex - ey;
        Vector2 p2 = center + ex + ey;
        Vector2 p3 = center - ex + ey;

        Line(p0, p1); Line(p1, p2); Line(p2, p3); Line(p3, p0);
    }

    public static void Arrow(Vector2 start, Vector2 dir, float length, float head = 0.08f)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        Vector2 end = start + dir * length;
        Line(start, end);

        Vector2 right = new Vector2(-dir.y, dir.x);
        Vector2 a = end - dir * head + right * head * 0.6f;
        Vector2 b = end - dir * head - right * head * 0.6f;

        Line(end, a);
        Line(end, b);
    }
}
