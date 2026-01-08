using UnityEngine;

[CreateAssetMenu(menuName = "MercMech/Combat/Impact VFX Config 2D", fileName = "ImpactVFXConfig2D")]
public class ImpactVFXConfig2D : ScriptableObject
{
    [Header("Prefab")]
    [Tooltip("Prefab to spawn on impact. Should be a ParticleSystem (root or child).")]
    public GameObject vfxPrefab;

    [Header("Behavior")]
    [Tooltip("Randomize Z rotation around the impact normal (degrees).")]
    public float randomAngleDegrees = 15f;

    [Tooltip("Scale multiplier applied to the spawned VFX transform.")]
    public float scale = 1f;

    [Header("Surface Alignment")]
    [Tooltip("If true, orient the VFX so its +X axis points along the impact normal (ie. sparks spray away from surface).")]
    public bool orientToNormal = true;

    public bool IsValid => vfxPrefab != null;
}