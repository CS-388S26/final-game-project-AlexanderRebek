using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// Slows enemies every few seconds

public class IceTower : MonoBehaviour
{
    [Header("Stats")]
    public float attackRange = 6f;
    public float freezeDuration = 2f;

    public float slowMultiplier = 0.3f;

    public float pulseCooldown = 3f;

    [Header("Wave visual")]
    public Color waveColor     = new Color(0.4f, 0.8f, 1f, 1f);
    public float waveWidth     = 0.12f;
    public int   waveSegments  = 64;

    public float waveExpandDuration = 0.4f;

    private float _cooldownTimer = 0f;

    private void Update()
    {
        _cooldownTimer += Time.deltaTime;
        if (_cooldownTimer >= pulseCooldown)
        {
            _cooldownTimer = 0f;
            Pulse();
        }
    }

    private void Pulse()
    {
        // Freeze all enemies in range
        List<EnemyController> targets = EnemyTargeter.Instance != null
            ? GetEnemiesInRange()
            : new List<EnemyController>();

        foreach (EnemyController e in targets)
            StartCoroutine(FreezeEnemy(e));

        // Play wave animation
        StartCoroutine(ExpandWave());
    }

    // Slows the enemy then restores its speed after freezeDuration
    private IEnumerator FreezeEnemy(EnemyController enemy)
    {
        if (enemy == null || enemy.IsDead) yield break;

        float originalSpeed = enemy.moveSpeed;
        enemy.moveSpeed = originalSpeed * slowMultiplier;

        yield return new WaitForSeconds(freezeDuration);

        // Only restore if still alive and not already frozen by another tower
        if (enemy != null && !enemy.IsDead)
            enemy.moveSpeed = originalSpeed;
    }

    // Expanding ring wave
    private IEnumerator ExpandWave()
    {
        GameObject waveGO = new GameObject("IceWave");
        LineRenderer lr   = waveGO.AddComponent<LineRenderer>();

        lr.useWorldSpace  = true;
        lr.loop           = true;
        lr.positionCount  = waveSegments;
        lr.widthMultiplier = waveWidth;
        lr.material       = new Material(Shader.Find("Sprites/Default"));
        lr.startColor     = waveColor;
        lr.endColor       = waveColor;
        lr.sortingOrder   = 10;

        float elapsed = 0f;
        Vector3 origin = new Vector3(transform.position.x, 0.2f, transform.position.z);

        while (elapsed < waveExpandDuration)
        {
            elapsed += Time.deltaTime;
            float t      = elapsed / waveExpandDuration;
            float radius = Mathf.Lerp(0f, attackRange, t);

            // Fade out as the wave expands
            Color c = waveColor;
            c.a = Mathf.Lerp(1f, 0f, t);
            lr.startColor = c;
            lr.endColor   = c;

            for (int i = 0; i < waveSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / waveSegments;
                lr.SetPosition(i, new Vector3(
                    origin.x + Mathf.Cos(angle) * radius,
                    0.2f,
                    origin.z + Mathf.Sin(angle) * radius));
            }

            yield return null;
        }

        Destroy(waveGO);
    }

    // Returns all living enemies in range
    private List<EnemyController> GetEnemiesInRange()
    {
        List<EnemyController> result = new List<EnemyController>();

        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange);
        foreach (Collider col in hits)
        {
            EnemyController e = col.GetComponentInParent<EnemyController>();
            if (e != null && !e.IsDead && !result.Contains(e))
                result.Add(e);
        }
        return result;
    }

    // Attack range gizmo
    private LineRenderer _lineRenderer;

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
        _lineRenderer.useWorldSpace  = true;
        _lineRenderer.loop           = true;
        _lineRenderer.widthMultiplier = 0.1f;
        _lineRenderer.positionCount  = 64;
        _lineRenderer.material       = new Material(Shader.Find("Sprites/Default"));
        _lineRenderer.startColor     = waveColor;
        _lineRenderer.endColor       = waveColor;
        _lineRenderer.sortingOrder   = 10;

        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI * 2f / 64;
            _lineRenderer.SetPosition(i, new Vector3(
                transform.position.x + Mathf.Cos(angle) * attackRange,
                0.2f,
                transform.position.z + Mathf.Sin(angle) * attackRange));
        }
        _lineRenderer.enabled = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif
}
