using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private bool spawnEnabled = true;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private int maxEnemies = 10;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(25f, 9f);
    [SerializeField] private Vector2 spawnAreaCenter = new Vector2(1f,2f);

    private float spawnTimer;
    private int currentEnemyCount;

    void Update()
    {
        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnInterval && currentEnemyCount < maxEnemies)
        {
            SpawnEnemy();
            spawnTimer = 0f;
        }
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning($"{nameof(EnemySpawner)}: No enemy prefab assigned!");
            return;
        }

        if (!spawnEnabled)
            return;

        // Random position within spawn area (half-extents)
        Vector2 randomOffset = new Vector2(
            Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f),
            Random.Range(-spawnAreaSize.y * 0.5f, spawnAreaSize.y * 0.5f)
        );

        Vector3 spawnPosition = (Vector2)transform.position + spawnAreaCenter + randomOffset;

        GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        
        // Track enemy count
        currentEnemyCount++;

        // Subscribe to health death event to decrement count
        var health = enemy.GetComponent<Health>();
        if (health != null)
        {
            health.OnDeath += OnEnemyDied;
        }
        else
        {
            Debug.LogWarning($"{nameof(EnemySpawner)}: Spawned enemy has no Health component!");
        }
    }

    private void OnEnemyDied()
    {
        currentEnemyCount--;
    }

    void OnDrawGizmosSelected()
    {
        // Draw spawn area
        Gizmos.color = Color.yellow;
        Vector3 center = (Vector2)transform.position + spawnAreaCenter;
        Gizmos.DrawWireCube(center, spawnAreaSize);

        // Draw spawn area corners
        Gizmos.color = Color.red;
        Vector3 topLeft = center + new Vector3(-spawnAreaSize.x * 0.5f, spawnAreaSize.y * 0.5f);
        Vector3 topRight = center + new Vector3(spawnAreaSize.x * 0.5f, spawnAreaSize.y * 0.5f);
        Vector3 bottomLeft = center + new Vector3(-spawnAreaSize.x * 0.5f, -spawnAreaSize.y * 0.5f);
        Vector3 bottomRight = center + new Vector3(spawnAreaSize.x * 0.5f, -spawnAreaSize.y * 0.5f);

        Gizmos.DrawSphere(topLeft, 0.2f);
        Gizmos.DrawSphere(topRight, 0.2f);
        Gizmos.DrawSphere(bottomLeft, 0.2f);
        Gizmos.DrawSphere(bottomRight, 0.2f);
    }
}