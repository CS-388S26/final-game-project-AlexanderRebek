using System.Collections;
using UnityEngine;


// Spawns enemies at the first checkpoint of the PathManager
public class EnemySpawner : MonoBehaviour
{
    [Header("References")]
    public PathManager pathManager;
    public GameObject enemyPrefab;

    [Header("Spawn settings")]
    [Min(0.1f)]
    public float spawnInterval = 2f;

    [Tooltip("Maximum number of enemies to spawn. 0 = infinite.")]
    public int maxEnemies = 0;

    private int _spawnedCount = 0;
    private bool _spawning = false;

    private void Start()
    {
        if (!ValidateReferences()) return;
        StartSpawning();
    }

    // Public API

    public void StartSpawning()
    {
        if (_spawning) return;
        _spawning = true;
        StartCoroutine(SpawnRoutine());
    }

    public void StopSpawning()
    {
        _spawning = false;
        StopAllCoroutines();
    }

    // Internal logic

    private IEnumerator SpawnRoutine()
    {
        while (_spawning)
        {
            if (maxEnemies > 0 && _spawnedCount >= maxEnemies)
            {
                _spawning = false;
                yield break;
            }

            SpawnEnemy();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnEnemy()
    {
        GameObject instance = Instantiate(enemyPrefab, pathManager.SpawnPosition, Quaternion.identity);

        EnemyController controller = instance.GetComponent<EnemyController>();
        if (controller != null)
            controller.Initialize(pathManager);
        else
            Debug.LogError("[EnemySpawner] Enemy prefab is missing an EnemyController component.");

        _spawnedCount++;
    }

    private bool ValidateReferences()
    {
        if (pathManager == null) { Debug.LogError("[EnemySpawner] No PathManager assigned."); return false; }
        if (enemyPrefab == null) { Debug.LogError("[EnemySpawner] No enemy prefab assigned."); return false; }
        if (pathManager.CheckpointCount < 2) { Debug.LogError("[EnemySpawner] PathManager needs at least 2 checkpoints."); return false; }
        return true;
    }
}
