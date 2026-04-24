using UnityEngine;


// Defines a valid placement area for turrets.
[RequireComponent(typeof(Collider))]
public class PlacementZone : MonoBehaviour
{
    [Header("Editor visual")]
    public Color gizmoColor = new Color(0.2f, 1f, 0.4f, 0.15f);

    [Header("Runtime visual")]
    public Color runtimeFillColor   = new Color(0.2f, 1f, 0.4f, 0.08f);
    public Color runtimeBorderColor = new Color(0.2f, 1f, 0.4f, 0.7f);
    public float borderWidth = 0.05f;

    [Tooltip("Extra height above the top face of the collider to avoid z-fighting with the ground.")]
    public float yOffset = 0.05f;

    private Collider _collider;
    private GameObject _runtimeVisual;
    private bool _visualBuilt = false;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
    }

    //Returns true if worldPoint is inside this zone collider bounds
    public bool Contains(Vector3 worldPoint)
    {
        // Transform the point into the collider's local space so rotation is respected (OBB check)
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        BoxCollider box = _collider as BoxCollider;
        if (box != null)
        {
            Vector3 half = box.size * 0.5f;
            Vector3 local = localPoint - box.center;
            return Mathf.Abs(local.x) <= half.x &&
                   Mathf.Abs(local.y) <= half.y &&
                   Mathf.Abs(local.z) <= half.z;
        }
        // Fallback for non-box colliders
        return _collider.bounds.Contains(worldPoint);
    }

    // Runtime show / hide called by TurretPlacer
    public void Show()
    {
        if (!_visualBuilt) BuildRuntimeVisual();
        if (_runtimeVisual) _runtimeVisual.SetActive(true);
    }

    public void Hide()
    {
        if (_runtimeVisual) _runtimeVisual.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_runtimeVisual) Destroy(_runtimeVisual);
    }

    // Builds a quad matching the debug 
    private void BuildRuntimeVisual()
    {
        BoxCollider box = _collider as BoxCollider;
        float w = box != null ? box.size.x : 1f;
        float d = box != null ? box.size.z : 1f;
        float localTopY = box != null ? box.center.y + box.size.y * 0.5f : 0.5f;

        _runtimeVisual = new GameObject("ZoneVisual");
        _runtimeVisual.transform.SetParent(transform, false);
        _runtimeVisual.transform.localPosition = new Vector3(box != null ? box.center.x : 0f, localTopY + yOffset, box != null ? box.center.z : 0f);
        _runtimeVisual.transform.localRotation = Quaternion.identity;
        _runtimeVisual.transform.localScale    = Vector3.one;

        // Filled quad
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(_runtimeVisual.transform, false);
        fill.AddComponent<MeshFilter>().mesh = BuildQuadMesh(w, d);
        fill.AddComponent<MeshRenderer>().material = BuildMaterial(runtimeFillColor, transparent: true);

        // Border frame
        GameObject border = new GameObject("Border");
        border.transform.SetParent(_runtimeVisual.transform, false);
        border.transform.localPosition = Vector3.up * 0.001f;
        border.AddComponent<MeshFilter>().mesh = BuildBorderMesh(w, d, borderWidth);
        border.AddComponent<MeshRenderer>().material = BuildMaterial(runtimeBorderColor, transparent: false);

        _runtimeVisual.SetActive(false);
        _visualBuilt = true;
    }

    private Mesh BuildQuadMesh(float w, float d)
    {
        float hw = w * 0.5f, hd = d * 0.5f;
        Mesh mesh = new Mesh();
        mesh.vertices  = new Vector3[] {
            new Vector3(-hw, 0, -hd), new Vector3( hw, 0, -hd),
            new Vector3( hw, 0,  hd), new Vector3(-hw, 0,  hd)
        };
        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        return mesh;
    }

    private Mesh BuildBorderMesh(float w, float d, float bw)
    {
        float hw = w * 0.5f, hd = d * 0.5f;
        float ihw = hw - bw, ihd = hd - bw;
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] {
            new Vector3(-hw, 0, -hd), new Vector3( hw, 0, -hd), new Vector3( hw, 0, -ihd), new Vector3(-hw, 0, -ihd),
            new Vector3(-hw, 0,  ihd),new Vector3( hw, 0,  ihd),new Vector3( hw, 0,  hd),  new Vector3(-hw, 0,  hd),
            new Vector3(-hw,  0, -ihd),new Vector3(-ihw, 0, -ihd),new Vector3(-ihw, 0, ihd),new Vector3(-hw,  0, ihd),
            new Vector3( ihw, 0, -ihd),new Vector3( hw,  0, -ihd),new Vector3( hw,  0, ihd),new Vector3( ihw, 0, ihd),
        };
        mesh.triangles = new int[] {
            0,2,1, 0,3,2,
            4,6,5, 4,7,6,
            8,10,9, 8,11,10,
            12,14,13, 12,15,14
        };
        mesh.RecalculateNormals();
        return mesh;
    }

    private Material BuildMaterial(Color color, bool transparent)
    {
        Material mat = new Material(Shader.Find("Unlit/Color"));
        if (transparent)
        {
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 4000;
        }
        else
        {
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 4001;
        }
        mat.color = color;
        return mat;
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
