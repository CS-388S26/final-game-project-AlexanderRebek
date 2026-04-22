using System.Collections.Generic;
using UnityEngine;

// Singleton that keeps track of all living enemies

public class EnemyTargeter : MonoBehaviour
{
    public static EnemyTargeter Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private readonly List<EnemyController> _enemies = new List<EnemyController>();

    // Registration

    public void Register(EnemyController enemy)
    {
        if (!_enemies.Contains(enemy)) _enemies.Add(enemy);
    }

    public void Unregister(EnemyController enemy)
    {
        _enemies.Remove(enemy);
    }

    // Query

    // Returns the enemy with the highest DistanceProgress within the given range, returns null if no enemy is in range
    public EnemyController GetMostAdvanced(Vector3 origin, float range)
    {
        EnemyController best = null;
        float bestDistance = -1f;
        float rangeSqr = range * range;

        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            if (_enemies[i] == null) { _enemies.RemoveAt(i); continue; }

            EnemyController e = _enemies[i];
            if (e.IsDead) continue;
            if ((e.transform.position - origin).sqrMagnitude > rangeSqr) continue;

            float d = e.DistanceProgress;
            if (d > bestDistance)
            {
                bestDistance = d;
                best = e;
            }
        }

        return best;
    }

    public int EnemyCount => _enemies.Count;
}
