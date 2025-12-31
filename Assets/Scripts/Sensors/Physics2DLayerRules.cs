using UnityEngine;

// TO-DO: Ensure this script is initialized early in the game (e.g., attach to a GameObject in the initial scene).
public class Physics2DLayerRules : MonoBehaviour
{
    [SerializeField] private string playerHurtboxLayer = "PlayerHurtbox";
    [SerializeField] private string enemyHurtboxLayer = "EnemyHurtbox";
    [SerializeField] private string playerProjectileLayer = "PlayerProjectile";
    [SerializeField] private string enemyProjectileLayer = "EnemyProjectile";

    private void Awake()
    {
        int pH = LayerMask.NameToLayer(playerHurtboxLayer);
        int eH = LayerMask.NameToLayer(enemyHurtboxLayer);
        int pP = LayerMask.NameToLayer(playerProjectileLayer);
        int eP = LayerMask.NameToLayer(enemyProjectileLayer);

        if (pH < 0 || eH < 0 || pP < 0 || eP < 0)
        {
            Debug.LogWarning("Physics2DLayerRules: missing one or more layers. Check layer names.");
            return;
        }

        Physics2D.IgnoreLayerCollision(pP, pH, true);
        Physics2D.IgnoreLayerCollision(eP, eH, true);

        // Optional:
        Physics2D.IgnoreLayerCollision(pP, pP, true);
        Physics2D.IgnoreLayerCollision(eP, eP, true);
    }
}
