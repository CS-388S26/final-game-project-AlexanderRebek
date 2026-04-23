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
    [Tooltip("Ground layer only — do NOT use Everything.")]
    public LayerMask groundLayerMask;
    public Camera cam;

    [Header("Ghost materials")]
    public Material validMaterial;
    public Material invalidMaterial;

    [Header("Turret collision")]
    public float turretRadius = 1f;

    [Header("Double tap")]
    public float doubleTapInterval = 0.3f;

    [Header("Range indicator — hold input")]
    [Tooltip("Seconds to hold on a turret before its range appears.")]
    public float holdDuration = 1f;
    [Tooltip("Normalised pressure (0-1) for immediate trigger on force-touch devices.")]
    [Range(0f, 1f)]
    public float pressureThreshold = 0.75f;

    // Placement state
    private GameObject _ghostInstance;
    private GameObject _pendingPrefab;
    private int _pendingCost;
    private bool _isPlacing = false;
    private float _ghostYOffset = 0f;   // Offset to lift the turret so its base sits on the ground

    // Double tap tracking
    private float _lastTapTime = -999f;
    private Vector2 _lastTapPosition;
    private const float DoubleTapMaxDistance = 40f;

    // Hold / range indicator state
    private bool _holding = false;
    private float _holdTimer = 0f;
    private TurretController _holdTarget;
    private TurretController _activeRangeTurret;

    private static readonly List<PlacedTurret> _placedTurrets = new List<PlacedTurret>();

    // Stores a placed turret alongside its purchase cost for refunding
    private class PlacedTurret
    {
        public TurretController controller;
        public int cost;
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
        _placedTurrets.RemoveAll(t => t.controller == null);

        if (_isPlacing)
            UpdatePlacementMode();
        else
            UpdateIdleMode();
    }

    // Placement mode update

    private void UpdatePlacementMode()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) { CancelPlacement(); return; }

#if UNITY_EDITOR || UNITY_STANDALONE
        UpdateGhostPosition(Input.mousePosition);
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI(Input.mousePosition))
            TryConfirmPlacement(Input.mousePosition);
        if (Input.GetMouseButtonDown(1))
            CancelPlacement();
#else
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            UpdateGhostPosition(touch.position);
            if (touch.phase == TouchPhase.Began && !IsPointerOverUI(touch.position))
                TryConfirmPlacement(touch.position);
        }

        // Second finger cancels
        if (Input.touchCount > 1)
            CancelPlacement();
#endif
    }

    // Double tap detection (when not in placement mode)

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
            TurretController hit = RaycastTurret(pos);

            if (hit != null && IsDoubleTap(pos))
            {
                RemoveTurret(hit);
                ResetDoubleTap();
                return;
            }
            RecordTap(pos);

            if (hit != null) BeginHold(hit);
            else             HideActiveRange();
        }

        if (Input.GetMouseButton(0) && _holding)
        {
            _holdTimer += Time.deltaTime;
            if (_holdTimer >= holdDuration) ShowRange(_holdTarget);
        }

        if (Input.GetMouseButtonUp(0))
        {
            HideActiveRange();
            CancelHold();
        }
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
            TurretController hit = RaycastTurret(touch.position);

            if (hit != null && IsDoubleTap(touch.position))
            {
                RemoveTurret(hit);
                ResetDoubleTap();
                return;
            }
            RecordTap(touch.position);

            if (hit != null)
            {
                BeginHold(hit);
                if (touch.pressure >= pressureThreshold) ShowRange(hit);
            }
            else HideActiveRange();
        }

        if ((touch.phase == TouchPhase.Stationary || touch.phase == TouchPhase.Moved) && _holding)
        {
            _holdTimer += Time.deltaTime;
            if (_holdTimer >= holdDuration || touch.pressure >= pressureThreshold)
                ShowRange(_holdTarget);
        }

        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            HideActiveRange();
            CancelHold();
        }
    }

    // Turret removal on double tap

    private bool IsDoubleTap(Vector2 pos) =>
        Time.unscaledTime - _lastTapTime <= doubleTapInterval &&
        Vector2.Distance(pos, _lastTapPosition) <= DoubleTapMaxDistance;

    private void RecordTap(Vector2 pos)
    {
        _lastTapTime     = Time.unscaledTime;
        _lastTapPosition = pos;
    }

    private void ResetDoubleTap() => _lastTapTime = -999f;

    // Hold helpers

    private void BeginHold(TurretController turret)
    {
        _holding    = true;
        _holdTimer  = 0f;
        _holdTarget = turret;
    }

    private void CancelHold()
    {
        _holding    = false;
        _holdTimer  = 0f;
        _holdTarget = null;
    }

    // Range indicator helpers

    private void ShowRange(TurretController turret)
    {
        if (turret == null || turret == _activeRangeTurret) return;
        HideActiveRange();
        turret.ShowRangeIndicator();
        _activeRangeTurret = turret;
    }

    private void HideActiveRange()
    {
        if (_activeRangeTurret == null) return;
        _activeRangeTurret.HideRangeIndicator();
        _activeRangeTurret = null;
    }

    // Turret removal

    private void RemoveTurret(TurretController tc)
    {
        PlacedTurret entry = _placedTurrets.Find(t => t.controller == tc);
        if (entry == null) return;
        TurretShopUI.Instance?.AddMoney(entry.cost);
        _placedTurrets.Remove(entry);
        Destroy(tc.gameObject);
    }

    // Public API

    public void BeginPlacement(GameObject turretPrefab, int cost)
    {
        if (_isPlacing) CancelPlacement();

        _pendingPrefab = turretPrefab;
        _pendingCost   = cost;
        _isPlacing     = true;

        ShowZones();

        _ghostInstance = Instantiate(turretPrefab, Vector3.zero, Quaternion.identity);
        // Calculate offset BEFORE disabling colliders — bounds returns zero on disabled colliders
        _ghostYOffset = CalculateGroundOffset(_ghostInstance);

        DisableGhostLogic(_ghostInstance);
    }

    public void CancelPlacement()
    {
        if (_ghostInstance != null) Destroy(_ghostInstance);
        _isPlacing     = false;
        _pendingPrefab = null;
        TurretShopUI.Instance?.AddMoney(_pendingCost);
        _pendingCost   = 0;
        HideZones();
    }

    // Zone visibility

    private void ShowZones()
    {
        foreach (PlacementZone z in zones) if (z != null) z.Show();
    }

    private void HideZones()
    {
        foreach (PlacementZone z in zones) if (z != null) z.Hide();
    }

    // Ghost positioning

    private void UpdateGhostPosition(Vector2 screenPos)
    {
        if (_ghostInstance == null) return;
        Vector3 groundHit = GetWorldPosition(screenPos);
        // Lift by the offset so the bottom of the collider sits exactly on the ground
        _ghostInstance.transform.position = groundHit + Vector3.up * _ghostYOffset;
        ApplyGhostMaterial(IsPlacementValid(groundHit));
    }

    private void TryConfirmPlacement(Vector2 screenPos)
    {
        Vector3 groundHit = GetWorldPosition(screenPos);
        if (IsPlacementValid(groundHit))
            ConfirmPlacement(groundHit);
    }

    private void ConfirmPlacement(Vector3 groundHit)
    {
        Destroy(_ghostInstance);
        _ghostInstance = null;

        // Place the turret lifted by the same offset used for the ghost
        Vector3 spawnPos = groundHit + Vector3.up * _ghostYOffset;
        GameObject turretGO = Instantiate(_pendingPrefab, spawnPos, Quaternion.identity);
        TurretController tc = turretGO.GetComponent<TurretController>();
        if (tc != null)
        {
            _placedTurrets.Add(new PlacedTurret { controller = tc, cost = _pendingCost });
        }

        _isPlacing     = false;
        _pendingPrefab = null;
        _pendingCost   = 0;
        HideZones();
    }

    // Calculates the Y offset needed to lift a prefab so its lowest collider
    // point sits at Y=0 (i.e. flush with the ground surface).
    private float CalculateGroundOffset(GameObject instance)
    {
        // Find the lowest point of all colliders relative to the object's pivot
        float lowestLocal = 0f;
        bool found = false;

        foreach (Collider col in instance.GetComponentsInChildren<Collider>())
        {
            // bounds.min.y is in world space; subtract the instance Y to get local offset
            float localBottom = col.bounds.min.y - instance.transform.position.y;
            if (!found || localBottom < lowestLocal)
            {
                lowestLocal = localBottom;
                found = true;
            }
        }

        // We want the bottom to be at 0, so we lift by -lowestLocal
        // (lowestLocal is negative when the bottom is below the pivot)
        return found ? -lowestLocal : 0f;
    }

    // Raycasting

    private Vector3 GetWorldPosition(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayerMask))
            return hit.point;

        // Fallback: Y=0 plane
        if (Mathf.Abs(ray.direction.y) > 0.0001f)
        {
            float t = -ray.origin.y / ray.direction.y;
            return ray.origin + ray.direction * t;
        }
        return Vector3.zero;
    }

    private TurretController RaycastTurret(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            return hit.collider.GetComponentInParent<TurretController>();
        return null;
    }

    private bool IsPlacementValid(Vector3 groundHit)
    {
        bool inZone = false;
        foreach (PlacementZone zone in zones)
        {
            if (zone != null && zone.Contains(groundHit)) { inZone = true; break; }
        }
        if (!inZone) return false;

        foreach (PlacedTurret t in _placedTurrets)
        {
            if (t.controller == null) continue;
            if (Vector3.Distance(groundHit, t.controller.transform.position) < turretRadius * 2f)
                return false;
        }
        return true;
    }

    private void DisableGhostLogic(GameObject ghost)
    {
        foreach (MonoBehaviour mb in ghost.GetComponentsInChildren<MonoBehaviour>())
            if (mb is TurretController) mb.enabled = false;

        // Disable colliders so the ghost doesn't block the ground raycast
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

    // Returns true if the screen position is over a UI element
    private bool IsPointerOverUI(Vector2 screenPos)
    {
        PointerEventData data = new PointerEventData(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);
        return results.Count > 0;
    }
}
