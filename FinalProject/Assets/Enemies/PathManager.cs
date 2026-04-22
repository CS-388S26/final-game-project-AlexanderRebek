using UnityEngine;

// Defines the path enemies will follow via an ordered array of transforms
public class PathManager : MonoBehaviour
{
    [Header("Checkpoints (in traversal order)")]
    [Tooltip("Drag the Transform of each checkpoint here in the order they should be visited.")]
    public Transform[] checkpoints;

    // Public API

    public int CheckpointCount => checkpoints.Length;

    public Transform GetCheckpoint(int index)
    {
        if (index < 0 || index >= checkpoints.Length)
        {
            Debug.LogWarning($"[PathManager] Index {index} out of range (0-{checkpoints.Length - 1}).");
            return null;
        }
        return checkpoints[index];
    }

    public Vector3 SpawnPosition => checkpoints.Length > 0 ? checkpoints[0].position : Vector3.zero;

    // Editor gizmos

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (checkpoints == null || checkpoints.Length == 0) return;

        for (int i = 0; i < checkpoints.Length; i++)
        {
            if (checkpoints[i] == null) continue;

            Gizmos.color = (i == 0) ? Color.green
                         : (i == checkpoints.Length - 1) ? Color.red
                         : Color.yellow;

            Gizmos.DrawSphere(checkpoints[i].position, 0.3f);

            if (i < checkpoints.Length - 1 && checkpoints[i + 1] != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(checkpoints[i].position, checkpoints[i + 1].position);
            }
        }
    }
#endif
}
