using UnityEngine;
using UnityEngine.Events;

public class EnemyController : MonoBehaviour
{
    [Header("Movement")]
    [Min(0.01f)]
    public float moveSpeed = 3f;
    public float waypointThreshold = 0.1f;

    [Header("Rotation")]
    public bool smoothRotation = true;
    public float rotationSpeed = 10f;

    [Header("Health")]
    [Min(1)]
    public float maxHealth = 100f;

    [Header("Events")]
    public UnityEvent OnReachedEnd;
    public UnityEvent<int> OnReachedCheckpoint;
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnDeath;

    private PathManager _pathManager;
    private int    _currentCheckpointIndex = 0;
    private bool   _moving  = false;
    private float  _currentHealth;
    private bool   _isDead  = false;

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
    }

    private void Awake()
    {
        _currentHealth = maxHealth;
    }

    private void Update()
    {
        if (!_moving || _isDead || _pathManager == null) return;
        MoveTowardsCurrentCheckpoint();
    }

    private void MoveTowardsCurrentCheckpoint()
    {
        Transform target = _pathManager.GetCheckpoint(_currentCheckpointIndex);
        if (target == null) return;

        Vector3 targetPos = target.position;
        float step = moveSpeed * Time.deltaTime;

        transform.position = Vector3.MoveTowards(transform.position, targetPos, step);

        if (smoothRotation)
        {
            Vector3 direction = targetPos - transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }

        if (Vector3.Distance(transform.position, targetPos) <= waypointThreshold)
            OnCheckpointReached();
    }

    private void OnCheckpointReached()
    {
        bool isLast = _currentCheckpointIndex >= _pathManager.CheckpointCount - 1;

        if (isLast)
        {
            _moving = false;
            OnReachedEnd?.Invoke();
            Destroy(gameObject);
        }
        else
        {
            OnReachedCheckpoint?.Invoke(_currentCheckpointIndex);
            _currentCheckpointIndex++;
        }
    }

    public void TakeDamage(float amount)
    {
        if (_isDead) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        OnHealthChanged?.Invoke(_currentHealth);

        if (_currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (_isDead) return;
        _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth);
    }

    private void Die()
    {
        _isDead = true;
        _moving = false;
        OnDeath?.Invoke();
        Destroy(gameObject);
    }

    public void SetMoving(bool active)      => _moving = active;
    public int   CurrentCheckpointIndex     => _currentCheckpointIndex;
    public float CurrentHealth              => _currentHealth;
    public float HealthPercent              => _currentHealth / maxHealth;
    public bool  IsDead                     => _isDead;
    public float PathProgress => _pathManager == null || _pathManager.CheckpointCount <= 1
        ? 0f
        : (float)_currentCheckpointIndex / (_pathManager.CheckpointCount - 1);
}
