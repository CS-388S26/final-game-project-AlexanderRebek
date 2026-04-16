using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public PathManager pathManager;
    public GameObject enemyPrefab;

    [Min(0.1f)]
    public float spawnInterval = 2f;

    public int maxEnemies = 0;

    private int _spawnedCount = 0;
    private bool _spawning = false;


    private void Start()
    {
        if (!ValidateReferences()) return;
        StartSpawning();
    }


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
        Vector3 spawnPos = pathManager.SpawnPosition;
        GameObject instance = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

        EnemyController controller = instance.GetComponent<EnemyController>();
        if (controller != null)
        {
            controller.Initialize(pathManager);
        }
        else
        {
            Debug.LogError("Prefab doesnt have enemycontroller");
        }

        _spawnedCount++;
    }

    private bool ValidateReferences()
    {
        if (pathManager == null)
        {
            Debug.LogError("No pathmanager");
            return false;
        }
        if (enemyPrefab == null)
        {
            Debug.LogError("No enemy");
            return false;
        }
        if (pathManager.CheckpointCount < 2)
        {
            Debug.LogError("Need more checkpoints");
            return false;
        }
        return true;
    }
}
