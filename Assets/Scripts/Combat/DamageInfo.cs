using UnityEngine;

public struct DamageInfo
{
    public float Amount;
    public Vector2 Point;
    public Vector2 Normal;
    public GameObject Source;
    public Team SourceTeam;

    public DamageInfo(float amount, Vector2 point, Vector2 normal, GameObject source, Team sourceTeam)
    {
        Amount = amount;
        Point = point;
        Normal = normal;
        Source = source;
        SourceTeam = sourceTeam;
    }
}
