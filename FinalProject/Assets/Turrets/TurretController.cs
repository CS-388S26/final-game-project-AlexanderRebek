using UnityEngine;

// Finds the most advanced enemy within attack range, rotates the whole turret
// toward it, and fires straight-line bullets at regular intervals.

public class TurretController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Point from which bullets are spawned (child of this GameObject).")]
    public Transform firePoint;

    [Tooltip("Bullet prefab (must have the Bullet script).")]
    public GameObject bulletPrefab;

    [Header("Stats")]
    public float attackRange = 8f;

    [Tooltip("Shots per second.")]
    [Min(0.01f)]
    public float fireRate = 1f;

    [Tooltip("Rotation speed in degrees per second.")]
    public float rotationSpeed = 120f;

    [Header("Target update")]
    [Tooltip("How often (seconds) the turret checks for a more advanced enemy in range.")]
    public float targetUpdateInterval = 0.25f;

    [Tooltip("How many more world units ahead a new enemy must be before the turret switches targets.")]
    public float targetSwitchThreshold = 1f;

    private float _fireCooldown = 0f;
    private float _targetUpdateTimer = 0f;
    private EnemyController _currentTarget;

    // Unity lifecycle

    private void Update()
    {
        _targetUpdateTimer -= Time.deltaTime;
        if (_targetUpdateTimer <= 0f)
        {
            RefreshTarget();
            _targetUpdateTimer = targetUpdateInterval;
        }

        if (_currentTarget == null) return;

        RotateTowardsTarget();

        _fireCooldown -= Time.deltaTime;
        if (_fireCooldown <= 0f)
        {
            Shoot();
            _fireCooldown = 1f / fireRate;
        }
    }

    // Target selection

    private void RefreshTarget()
    {
        if (EnemyTargeter.Instance == null) return;

        // Validate current target
        bool currentTargetValid = _currentTarget != null
            && !_currentTarget.IsDead
            && (transform.position - _currentTarget.transform.position).sqrMagnitude <= attackRange * attackRange;

        EnemyController best = EnemyTargeter.Instance.GetMostAdvanced(transform.position, attackRange);

        if (best == null)
        {
            _currentTarget = null;
            return;
        }

        if (!currentTargetValid)
        {
            // No valid target — take whatever is most advanced
            _currentTarget = best;
            return;
        }

        // Only switch if the new candidate is ahead by more than the threshold
        float gain = best.DistanceProgress - _currentTarget.DistanceProgress;
        if (gain > targetSwitchThreshold)
            _currentTarget = best;
    }

    // Internal logic

    private void RotateTowardsTarget()
    {
        Vector3 direction = _currentTarget.transform.position - transform.position;
        direction.y = 0f;
        if (direction == Vector3.zero) return;

        Quaternion targetRot = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    private void Shoot()
    {
        if (bulletPrefab == null || firePoint == null || _currentTarget == null) return;

        GameObject bulletGO = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Bullet bullet = bulletGO.GetComponent<Bullet>();

        if (bullet != null)
            bullet.Fire(_currentTarget.transform.position, _currentTarget);
        else
            Debug.LogError("[TurretController] Bullet prefab is missing the Bullet component.");
    }

    // Attack range gizmo

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif
}
