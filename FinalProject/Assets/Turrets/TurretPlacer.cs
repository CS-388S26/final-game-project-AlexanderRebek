using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Handles turret placement and removal
public class TurretPlacer : MonoBehaviour
{
    public static TurretPlacer Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    [Header("Placement zones")]
    public PlacementZone[] zones;

    [Header("Raycast")]
    public LayerMask groundLayerMask;
    public Camera cam;

    [Header("Ghost materials")]
    public Material validMaterial;
    public Material invalidMaterial;

    [Header("Turret collision")]
    public float turretRadius = 1f;

    [Header("Double tap")]
    public float doubleTapInterval = 0.3f;

    [Header("Range indicator")]
    public float holdDuration = 1f;
    [Range(0f, 1f)]
    public float pressureThreshold = 0.75f;

    // Placement state
    private GameObject _ghostInstance;
    private LineRenderer _ghostRange;
    private GameObject _pendingPrefab;
    private int  _pendingCost;
    private bool _isPlacing = false;
    private bool _isDragging = false;
    private float _ghostYOffset = 0f;

    // Double tap state
    private float   _lastTapTime     = -999f;
    private Vector2 _lastTapPosition;
    private const float DoubleTapMaxDistance = 40f;

    // Hold state (placed turrets)
    private bool  _holding    = false;
    private float _holdTimer  = 0f;
    private GameObject _holdTarget;
    private GameObject _activeRangeTarget;

    private static readonly List<PlacedTurret> _placedTurrets = new List<PlacedTurret>();

    private class PlacedTurret
    {
        public GameObject go;
        public int        cost;
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
        _placedTurrets.RemoveAll(t => t.go == null);

        if (_isPlacing)
            UpdatePlacementMode();
        else
            UpdateIdleMode();
    }

    // Placement mode

    private void UpdatePlacementMode()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) { CancelPlacement(); return; }

#if UNITY_EDITOR || UNITY_STANDALONE
        UpdateGhostPosition(Input.mousePosition);

        if (!_isDragging)
        {
            // Waiting for first click on the map
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI(Input.mousePosition))
                _isDragging = true;
            if (Input.GetMouseButtonDown(1))
                CancelPlacement();
        }
        else
        {
            // Follow mouse until released
            if (Input.GetMouseButtonUp(0))
                TryConfirmPlacement(Input.mousePosition);
            if (Input.GetMouseButtonDown(1))
                CancelPlacement();
        }
#else
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            UpdateGhostPosition(touch.position);

            if (!_isDragging)
            {
                // Waiting for first tap on the map
                if (touch.phase == TouchPhase.Began && !IsPointerOverUI(touch.position))
                    _isDragging = true;
            }
            else
            {
                // Follow finger until released
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    TryConfirmPlacement(touch.position);
            }
        }
        if (Input.touchCount > 1) CancelPlacement();
#endif
    }

    // Idle mode

    private void UpdateIdleMode()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseIdle();
#else
        HandleTouchIdle();
#endif
    }

    private void HandleMouseIdle()
    {
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI(Input.mousePosition))
        {
            Vector2 pos = Input.mousePosition;
            GameObject hit = RaycastTurretGO(pos);

            if (hit != null && IsDoubleTap(pos)) { RemoveTurret(hit); ResetDoubleTap(); return; }
            RecordTap(pos);

            if (hit != null) BeginHold(hit);
            else             HideActiveRange();
        }

        if (Input.GetMouseButton(0) && _holding)
        {
            _holdTimer += Time.deltaTime;
            if (_holdTimer >= holdDuration) ShowRange(_holdTarget);
        }

        if (Input.GetMouseButtonUp(0)) { HideActiveRange(); CancelHold(); }
    }

    private void HandleTouchIdle()
    {
        if (Input.touchCount == 0)
        {
            if (_holding) { HideActiveRange(); CancelHold(); }
            return;
        }

        Touch touch = Input.GetTouch(0);
        if (IsPointerOverUI(touch.position)) return;

        if (touch.phase == TouchPhase.Began)
        {
            GameObject hit = RaycastTurretGO(touch.position);
            if (hit != null && IsDoubleTap(touch.position)) { RemoveTurret(hit); ResetDoubleTap(); return; }
            RecordTap(touch.position);
            if (hit != null) { BeginHold(hit); if (touch.pressure >= pressureThreshold) ShowRange(hit); }
            else HideActiveRange();
        }

        if ((touch.phase == TouchPhase.Stationary || touch.phase == TouchPhase.Moved) && _holding)
        {
            _holdTimer += Time.deltaTime;
            if (_holdTimer >= holdDuration || touch.pressure >= pressureThreshold) ShowRange(_holdTarget);
        }

        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            HideActiveRange(); CancelHold();
        }
    }

    // Ghost

    private void UpdateGhostPosition(Vector2 screenPos)
    {
        if (_ghostInstance == null) return;
        Vector3 groundHit = GetWorldPosition(screenPos);
        _ghostInstance.transform.position = groundHit + Vector3.up * _ghostYOffset;
        ApplyGhostMaterial(IsPlacementValid(groundHit));
        UpdateGhostRange(groundHit + Vector3.up * _ghostYOffset);
    }

    private void TryConfirmPlacement(Vector2 screenPos)
    {
        Vector3 groundHit = GetWorldPosition(screenPos);
        if (IsPlacementValid(groundHit))
            ConfirmPlacement(groundHit);
        else
            CancelPlacement();
    }

    // Ghost range circle

    private void BuildGhostRange(GameObject ghost, float range)
    {
        _ghostRange = ghost.AddComponent<LineRenderer>();
        _ghostRange.useWorldSpace   = true;
        _ghostRange.loop            = true;
        _ghostRange.widthMultiplier = 0.1f;
        _ghostRange.positionCount   = 64;
        _ghostRange.material        = new Material(Shader.Find("Sprites/Default"));
        _ghostRange.startColor      = Color.white;
        _ghostRange.endColor        = Color.white;
        _ghostRange.sortingOrder    = 10;
        UpdateGhostRange(ghost.transform.position);
    }

    private void UpdateGhostRange(Vector3 center)
    {
        if (_ghostRange == null) return;
        float range = GetAttackRange(_pendingPrefab);

        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI * 2f / 64;
            _ghostRange.SetPosition(i, new Vector3(
                center.x + Mathf.Cos(angle) * range,
                0.2f,
                center.z + Mathf.Sin(angle) * range));
        }
    }

    // Public API

    public void BeginPlacement(GameObject turretPrefab, int cost)
    {
        if (_isPlacing) CancelPlacement();

        _pendingPrefab = turretPrefab;
        _pendingCost   = cost;
        _isPlacing     = true;
        _isDragging    = false;

        ShowZones();

        _ghostInstance = Instantiate(turretPrefab, Vector3.zero, Quaternion.identity);
        _ghostYOffset  = CalculateGroundOffset(_ghostInstance);
        DisableGhostLogic(_ghostInstance);

        BuildGhostRange(_ghostInstance, GetAttackRange(turretPrefab));
    }

    public void CancelPlacement()
    {
        if (_ghostInstance != null) Destroy(_ghostInstance);
        _ghostInstance = null;
        _ghostRange    = null;
        _isPlacing     = false;
        _isDragging    = false;
        _pendingPrefab = null;
        TurretShopUI.Instance?.AddMoney(_pendingCost);
        _pendingCost   = 0;
        HideZones();
    }

    // Zone visibility

    private void ShowZones() { foreach (PlacementZone z in zones) if (z != null) z.Show(); }
    private void HideZones() { foreach (PlacementZone z in zones) if (z != null) z.Hide(); }

    // Confirm

    private void ConfirmPlacement(Vector3 groundHit)
    {
        Destroy(_ghostInstance);
        _ghostInstance = null;
        _ghostRange    = null;

        Vector3 spawnPos = groundHit + Vector3.up * _ghostYOffset;
        GameObject turretGO = Instantiate(_pendingPrefab, spawnPos, Quaternion.identity);
        _placedTurrets.Add(new PlacedTurret { go = turretGO, cost = _pendingCost });

        _isPlacing     = false;
        _isDragging    = false;
        _pendingPrefab = null;
        _pendingCost   = 0;
        HideZones();
    }

    // Double tap helpers

    private bool IsDoubleTap(Vector2 pos) =>
        Time.unscaledTime - _lastTapTime <= doubleTapInterval &&
        Vector2.Distance(pos, _lastTapPosition) <= DoubleTapMaxDistance;

    private void RecordTap(Vector2 pos) { _lastTapTime = Time.unscaledTime; _lastTapPosition = pos; }
    private void ResetDoubleTap() => _lastTapTime = -999f;

    // Hold helpers

    private void BeginHold(GameObject go) { _holding = true; _holdTimer = 0f; _holdTarget = go; }
    private void CancelHold() { _holding = false; _holdTimer = 0f; _holdTarget = null; }

    // Range indicator (placed turrets)

    private void ShowRange(GameObject go)
    {
        if (go == null || go == _activeRangeTarget) return;
        HideActiveRange();
        go.GetComponent<TurretController>()?.ShowRangeIndicator();
        go.GetComponent<IceTower>()?.ShowRangeIndicator();
        _activeRangeTarget = go;
    }

    private void HideActiveRange()
    {
        if (_activeRangeTarget == null) return;
        _activeRangeTarget.GetComponent<TurretController>()?.HideRangeIndicator();
        _activeRangeTarget.GetComponent<IceTower>()?.HideRangeIndicator();
        _activeRangeTarget = null;
    }

    // Turret removal

    private void RemoveTurret(GameObject go)
    {
        PlacedTurret entry = _placedTurrets.Find(t => t.go == go);
        if (entry == null) return;
        TurretShopUI.Instance?.AddMoney(entry.cost);
        _placedTurrets.Remove(entry);
        Destroy(go);
    }

    // Raycasting

    private Vector3 GetWorldPosition(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayerMask))
            return hit.point;

        if (Mathf.Abs(ray.direction.y) > 0.0001f)
        {
            float t = -ray.origin.y / ray.direction.y;
            return ray.origin + ray.direction * t;
        }
        return Vector3.zero;
    }

    private GameObject RaycastTurretGO(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f)) return null;
        TurretController tc = hit.collider.GetComponentInParent<TurretController>();
        if (tc != null) return tc.gameObject;
        IceTower ice = hit.collider.GetComponentInParent<IceTower>();
        if (ice != null) return ice.gameObject;
        return null;
    }

    private bool IsPlacementValid(Vector3 groundHit)
    {
        bool inZone = false;
        foreach (PlacementZone zone in zones)
            if (zone != null && zone.Contains(groundHit)) { inZone = true; break; }
        if (!inZone) return false;

        foreach (PlacedTurret t in _placedTurrets)
        {
            if (t.go == null) continue;
            if (Vector3.Distance(groundHit, t.go.transform.position) < turretRadius * 2f)
                return false;
        }
        return true;
    }

    private float GetAttackRange(GameObject go)
    {
        if (go == null) return 5f;
        TurretController tc = go.GetComponent<TurretController>();
        if (tc != null) return tc.attackRange;
        IceTower ice = go.GetComponent<IceTower>();
        if (ice != null) return ice.attackRange;
        return 5f;
    }

    private float CalculateGroundOffset(GameObject instance)
    {
        float lowestLocal = 0f;
        bool found = false;
        foreach (Collider col in instance.GetComponentsInChildren<Collider>())
        {
            float localBottom = col.bounds.min.y - instance.transform.position.y;
            if (!found || localBottom < lowestLocal) { lowestLocal = localBottom; found = true; }
        }
        return found ? -lowestLocal : 0f;
    }

    private void DisableGhostLogic(GameObject ghost)
    {
        foreach (MonoBehaviour mb in ghost.GetComponentsInChildren<MonoBehaviour>())
            if (mb is TurretController || mb is IceTower) mb.enabled = false;
        foreach (Collider col in ghost.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    private void ApplyGhostMaterial(bool valid)
    {
        if (_ghostInstance == null) return;
        Material mat = valid ? validMaterial : invalidMaterial;
        if (mat == null) return;
        foreach (Renderer r in _ghostInstance.GetComponentsInChildren<Renderer>())
            r.material = mat;
    }

    private bool IsPointerOverUI(Vector2 screenPos)
    {
        PointerEventData data = new PointerEventData(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);
        return results.Count > 0;
    }
}
