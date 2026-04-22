using UnityEngine;


// Defines a valid placement area for turrets.
[RequireComponent(typeof(Collider))]
public class PlacementZone : MonoBehaviour
{
    [Header("Editor visual")]
    public Color gizmoColor = new Color(0.2f, 1f, 0.4f, 0.15f);

    private Collider _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
    }

    //Returns true if worldPoint is inside this zone collider bounds
    public bool Contains(Vector3 worldPoint)
    {
        return _collider.bounds.Contains(worldPoint);
    }

    // Editor gizmo

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.7f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
#endif
}
