using UnityEngine;

// Finds the most advanced enemy within attack range, rotates the whole turret
// toward it, and fires straight-line bullets at regular intervals.

public class TurretController : MonoBehaviour
{
    [Header("References")]
    public Transform firePoint;
    public GameObject bulletPrefab;

    [Header("Stats")]
    public float attackRange = 8f;
    [Min(0.01f)] public float fireRate = 1f;
    public float rotationSpeed = 120f;

    [Header("Target update")]
    public float targetUpdateInterval = 0.25f;
    public float targetSwitchThreshold = 1f;

    [Header("Range indicator")]
    public Color rangeColor = Color.yellow;
    public float rangeLineWidth = 0.1f;

    private float _fireCooldown = 0f;
    private float _targetUpdateTimer = 0f;
    private EnemyController _currentTarget;
    private LineRenderer _lineRenderer;

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

    private void RotateTowardsTarget()
    {
        Vector3 direction = _currentTarget.transform.position - transform.position;
        direction.y = 0f;
        if (direction == Vector3.zero) return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, Quaternion.LookRotation(direction), rotationSpeed * Time.deltaTime);
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

    public void ShowRangeIndicator()
    {
        if (_lineRenderer == null) BuildLineRenderer();
        _lineRenderer.enabled = true;
    }

    public void HideRangeIndicator()
    {
        if (_lineRenderer != null) _lineRenderer.enabled = false;
    }

    private void BuildLineRenderer()
    {
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.loop = true;
        _lineRenderer.widthMultiplier = rangeLineWidth;
        _lineRenderer.positionCount = 64;
        _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _lineRenderer.startColor = rangeColor;
        _lineRenderer.endColor   = rangeColor;
        _lineRenderer.sortingOrder = 10;

        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI * 2f / 64;
            float x = transform.position.x + Mathf.Cos(angle) * attackRange;
            float z = transform.position.z + Mathf.Sin(angle) * attackRange;
            _lineRenderer.SetPosition(i, new Vector3(x, 0.2f, z));
        }

        _lineRenderer.enabled = false;
    }

    // Editor gizmo

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif
}
