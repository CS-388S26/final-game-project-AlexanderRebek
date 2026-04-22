using UnityEngine;
using UnityEngine.Events;

// Moves the enemy along the path defined by PathManager and manages its health.
public class EnemyController : MonoBehaviour
{
    [Header("Movement")]
    [Min(0.01f)] public float moveSpeed = 3f;
    public float waypointThreshold = 0.1f;

    [Header("Rotation")]
    public bool smoothRotation = true;
    public float rotationSpeed = 10f;

    [Header("Health")]
    [Min(1)] public float maxHealth = 100f;

    [Header("Movement events")]
    public UnityEvent onReachedEnd;
    public UnityEvent<int> onReachedCheckpoint;

    [Header("Health events")]
    public UnityEvent<float> onHealthChanged;
    public UnityEvent onDeath;

    private PathManager _pathManager;
    private int _currentCheckpointIndex = 0;
    private bool _moving = false;
    private float _currentHealth;
    private bool _isDead = false;

    // Unity lifecycle

    private void Awake()
    {
        _currentHealth = maxHealth;
    }

    private void Start()
    {
        EnemyTargeter.Instance?.Register(this);
    }

    private void OnDestroy()
    {
        EnemyTargeter.Instance?.Unregister(this);
    }

    private void Update()
    {
        if (!_moving || _isDead || _pathManager == null) return;
        MoveTowardsCurrentCheckpoint();
    }

    // Initialization called by EnemySpawner

    public void Initialize(PathManager pathManager)
    {
        _pathManager = pathManager;
        _currentCheckpointIndex = 0;
        _currentHealth = maxHealth;
        _isDead = false;

        transform.position = _pathManager.GetCheckpoint(0).position;

        if (_pathManager.CheckpointCount > 1)
        {
            Vector3 dir = _pathManager.GetCheckpoint(1).position - transform.position;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        _moving = true;

        EnemyTargeter.Instance?.Register(this);
    }

    // Movement

    private void MoveTowardsCurrentCheckpoint()
    {
        Transform target = _pathManager.GetCheckpoint(_currentCheckpointIndex);
        if (target == null) return;

        Vector3 targetPos = target.position;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);

        if (smoothRotation)
        {
            Vector3 dir = targetPos - transform.position;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
        }

        if (Vector3.Distance(transform.position, targetPos) <= waypointThreshold)
            OnCheckpointReached();
    }

    private void OnCheckpointReached()
    {
        if (_currentCheckpointIndex >= _pathManager.CheckpointCount - 1)
        {
            _moving = false;
            onReachedEnd?.Invoke();
            Destroy(gameObject);
        }
        else
        {
            onReachedCheckpoint?.Invoke(_currentCheckpointIndex);
            _currentCheckpointIndex++;
        }
    }

    // Health

    public void TakeDamage(float amount)
    {
        if (_isDead) return;
        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        onHealthChanged?.Invoke(_currentHealth);
        if (_currentHealth <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (_isDead) return;
        _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
        onHealthChanged?.Invoke(_currentHealth);
    }

    private void Die()
    {
        _isDead = true;
        _moving = false;
        onDeath?.Invoke();
        Destroy(gameObject);
    }

    // Public API

    public void SetMoving(bool active) => _moving = active;
    public int CurrentCheckpointIndex => _currentCheckpointIndex;
    public float CurrentHealth => _currentHealth;
    public float HealthPercent => _currentHealth / maxHealth;
    public bool IsDead => _isDead;

    //Track progress (0-1) based on checkpoint index
    public float PathProgress => _pathManager == null || _pathManager.CheckpointCount <= 1
        ? 0f
        : (float)_currentCheckpointIndex / (_pathManager.CheckpointCount - 1);


    // Precise progress in world units traveled along the path.
    public float DistanceProgress
    {
        get
        {
            if (_pathManager == null || _pathManager.CheckpointCount < 2) return 0f;

            // Sum all fully completed segments
            float total = 0f;
            for (int i = 0; i < _currentCheckpointIndex; i++)
            {
                Transform a = _pathManager.GetCheckpoint(i);
                Transform b = _pathManager.GetCheckpoint(i + 1);
                if (a != null && b != null)
                    total += Vector3.Distance(a.position, b.position);
            }

            // Add distance already traveled inside the current segment
            Transform current = _pathManager.GetCheckpoint(_currentCheckpointIndex);
            Transform next = _pathManager.GetCheckpoint(
                Mathf.Min(_currentCheckpointIndex + 1, _pathManager.CheckpointCount - 1));

            if (current != null && next != null)
            {
                float segmentLength = Vector3.Distance(current.position, next.position);
                float distToNext    = Vector3.Distance(transform.position, next.position);
                total += Mathf.Max(0f, segmentLength - distToNext);
            }

            return total;
        }
    }
}
