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
    [Tooltip("Set this to your ground layer only. Do NOT use Everything.")]
    public LayerMask groundLayerMask;
    public Camera cam;

    [Header("Ghost materials")]
    public Material validMaterial;
    public Material invalidMaterial;

    [Header("Turret collision")]
    public float turretRadius = 1f;

    [Header("Double tap settings")]
    [Tooltip("Maximum seconds between two taps to count as a double tap.")]
    public float doubleTapInterval = 0.3f;

    private GameObject _ghostInstance;
    private GameObject _pendingPrefab;
    private int _pendingCost;
    private bool _isPlacing = false;

    // Double tap tracking
    private float _lastTapTime = -999f;
    private Vector2 _lastTapPosition;
    private const float DoubleTapMaxDistance = 40f; // pixels

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
            CheckForDoubleTap();
    }

    // Placement mode update

    private void UpdatePlacementMode()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
            return;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        UpdateGhostPosition(Input.mousePosition);

        // Left click on the map (not on UI) confirms placement
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI(Input.mousePosition))
            TryConfirmPlacement(Input.mousePosition);

        // Right click cancels
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

    private void CheckForDoubleTap()
    {
        Vector2 tapPos = Vector2.zero;
        bool tapped = false;

#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI(Input.mousePosition))
        {
            tapPos = Input.mousePosition;
            tapped = true;
        }
#else
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began && !IsPointerOverUI(touch.position))
            {
                tapPos = touch.position;
                tapped = true;
            }
        }
#endif

        if (!tapped) return;

        float timeSinceLast = Time.unscaledTime - _lastTapTime;
        float distFromLast  = Vector2.Distance(tapPos, _lastTapPosition);

        if (timeSinceLast <= doubleTapInterval && distFromLast <= DoubleTapMaxDistance)
        {
            // Double tap — check if it hit a placed turret
            TryRemoveTurretAt(tapPos);
            _lastTapTime = -999f; // Reset so a third tap doesn't trigger again
        }
        else
        {
            _lastTapTime     = Time.unscaledTime;
            _lastTapPosition = tapPos;
        }
    }

    // Turret removal on double tap

    private void TryRemoveTurretAt(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f)) return;

        TurretController tc = hit.collider.GetComponentInParent<TurretController>();
        if (tc == null) return;

        PlacedTurret entry = _placedTurrets.Find(t => t.controller == tc);
        if (entry == null) return;

        TurretShopUI.Instance?.AddMoney(entry.cost);
        _placedTurrets.Remove(entry);
        Destroy(tc.gameObject);
    }

    // Ghost

    private void UpdateGhostPosition(Vector2 screenPos)
    {
        if (_ghostInstance == null) return;

        Vector3 worldPos = GetWorldPosition(screenPos);
        _ghostInstance.transform.position = worldPos;
        ApplyGhostMaterial(IsPlacementValid(worldPos));
    }

    private void TryConfirmPlacement(Vector2 screenPos)
    {
        Vector3 worldPos = GetWorldPosition(screenPos);

        if (IsPlacementValid(worldPos))
            ConfirmPlacement(worldPos);
        else
            Debug.Log("[TurretPlacer] Invalid placement position.");
    }

    // Public API

    // Called by TurretShopUI when a shop button is tapped
    public void BeginPlacement(GameObject turretPrefab, int cost)
    {
        if (_isPlacing) CancelPlacement();

        _pendingPrefab = turretPrefab;
        _pendingCost   = cost;
        _isPlacing     = true;

        _ghostInstance = Instantiate(turretPrefab, Vector3.zero, Quaternion.identity);
        DisableGhostLogic(_ghostInstance);
    }

    //Cancels placement and refunds the cost
    public void CancelPlacement()
    {
        if (_ghostInstance != null) Destroy(_ghostInstance);
        _isPlacing     = false;
        _pendingPrefab = null;
        TurretShopUI.Instance?.AddMoney(_pendingCost);
        _pendingCost   = 0;
    }

    // Internal logic

    private void ConfirmPlacement(Vector3 position)
    {
        Destroy(_ghostInstance);
        _ghostInstance = null;

        GameObject turretGO = Instantiate(_pendingPrefab, position, Quaternion.identity);
        TurretController tc = turretGO.GetComponent<TurretController>();

        if (tc != null)
            _placedTurrets.Add(new PlacedTurret { controller = tc, cost = _pendingCost });

        _isPlacing     = false;
        _pendingPrefab = null;
        _pendingCost   = 0;
    }

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

    private bool IsPlacementValid(Vector3 position)
    {
        bool inZone = false;
        foreach (PlacementZone zone in zones)
        {
            if (zone != null && zone.Contains(position)) { inZone = true; break; }
        }
        if (!inZone) return false;

        foreach (PlacedTurret t in _placedTurrets)
        {
            if (t.controller == null) continue;
            if (Vector3.Distance(position, t.controller.transform.position) < turretRadius * 2f)
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
        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPos
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }
}
