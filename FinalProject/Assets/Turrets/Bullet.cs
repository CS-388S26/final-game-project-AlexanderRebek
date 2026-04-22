using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Stats")]
    public float speed = 15f;
    public float damage = 25f;
    public float maxLifetime = 5f;

    private Vector3 _direction;
    private EnemyController _intendedTarget;
    private float _timer;

    // Called by TurretController right after Instantiate
    public void Fire(Vector3 targetPosition, EnemyController target)
    {
        _intendedTarget = target;
        _direction = (targetPosition - transform.position).normalized;

        if (_direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(_direction);
    }

    // Unity lifecycle
    private void Update()
    {
        transform.position += _direction * speed * Time.deltaTime;

        _timer += Time.deltaTime;
        if (_timer >= maxLifetime)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        EnemyController enemy = other.GetComponent<EnemyController>();

        // Only damage the intended target, ignore everything else
        if (enemy == null || enemy != _intendedTarget) return;

        enemy.TakeDamage(damage);
        Destroy(gameObject);
    }
}
