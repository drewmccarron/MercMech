using UnityEngine;

public class DebugSettings : MonoBehaviour
{
    public static bool Enabled { get; private set; }

    [SerializeField] private bool startEnabled = true;

    private void Awake()
    {
        Enabled = startEnabled;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Enabled = startEnabled;
    }
#endif
}
