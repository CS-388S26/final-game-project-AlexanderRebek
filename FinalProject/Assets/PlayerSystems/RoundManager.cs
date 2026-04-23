using System.Collections;
using UnityEngine;


// Manages infinite round progression
public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    [Header("References")]
    public EnemySpawner enemySpawner;
    public PlayerHealth playerHealth;
    public GameUI       gameUI;

    [Header("Round timing")]
    [Tooltip("Seconds of prep time between rounds where no enemies spawn.")]
    public float breakBetweenRounds = 8f;

    [Header("Round scaling — base values (round 1)")]
    public int   baseEnemyCount    = 8;
    public float baseSpawnInterval = 2f;
    public float baseEnemyHealth   = 100f;
    public float baseEnemySpeed    = 2f;
    public int   baseMoney         = 150;

    [Header("Round scaling — increase per round")]
    public int   enemyCountPerRound     = 4;
    public float spawnIntervalReduction = 0.15f;
    public float healthPerRound         = 25f;
    public float speedPerRound          = 0.1f;
    public int   moneyPerRound          = 50;

    public int  CurrentRound    { get; private set; } = 0;
    public bool RoundInProgress { get; private set; } = false;

    private int _enemiesRemainingThisRound = 0;

    private void Start()
    {
        StartCoroutine(GameLoop());
    }

    private IEnumerator GameLoop()
    {
        while (!playerHealth.IsDead)
        {
            RoundInProgress = false;
            int moneyThisBreak = baseMoney + CurrentRound * moneyPerRound;
            TurretShopUI.Instance?.AddMoney(moneyThisBreak);
            gameUI?.UpdateRound(CurrentRound, inBreak: true, breakDuration: breakBetweenRounds);
            yield return new WaitForSeconds(breakBetweenRounds);

            if (playerHealth.IsDead) yield break;

            // Start next round
            CurrentRound++;
            RoundInProgress = true;

            int   enemyCount    = baseEnemyCount + (CurrentRound - 1) * enemyCountPerRound;
            float spawnInterval = Mathf.Max(0.4f, baseSpawnInterval - (CurrentRound - 1) * spawnIntervalReduction);
            float enemyHealth   = baseEnemyHealth + (CurrentRound - 1) * healthPerRound;
            float enemySpeed    = baseEnemySpeed + (CurrentRound - 1) * speedPerRound;

            _enemiesRemainingThisRound = enemyCount;

            gameUI?.UpdateRound(CurrentRound, inBreak: false, breakDuration: 0);

            enemySpawner.ConfigureRound(enemyCount, spawnInterval, enemyHealth, enemySpeed);
            enemySpawner.StartSpawning();

            yield return new WaitUntil(() => _enemiesRemainingThisRound <= 0);

            enemySpawner.StopSpawning();
        }
    }

    public void OnEnemyRemoved()
    {
        _enemiesRemainingThisRound = Mathf.Max(0, _enemiesRemainingThisRound - 1);
    }
}
