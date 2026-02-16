using UnityEngine;

public class ReplicatorNode : MonoBehaviour
{
    public GridManager2D grid;

    [Header("Grid Pos (derived from transform)")]
    public int x;
    public int y;

    [Header("Ports (must be perpendicular)")]
    public Vector2Int entryDir = Vector2Int.down;  // Entry direction relative to replicator (example: down).
    public Vector2Int exitDir = Vector2Int.right; // Exit direction relative to replicator (example: right).

    private void Start()
    {
        if (grid == null) grid = FindObjectOfType<GridManager2D>();
        SyncFromWorld();
    }

    [ContextMenu("Sync From World")]
    public void SyncFromWorld()
    {
        if (grid == null) return;
        Vector3 p = transform.position;
        x = Mathf.RoundToInt(p.x / grid.cellSize);
        y = Mathf.RoundToInt(p.z / grid.cellSize);
    }

    public Vector2Int EntryCell => new Vector2Int(x + entryDir.x, y + entryDir.y);
    public Vector2Int ExitCell => new Vector2Int(x + exitDir.x, y + exitDir.y);

    /// <summary>
    /// Compute the 90-degree rotation from entry direction to exit direction (perpendicular only).
    /// Returns: +1 = CCW(90), -1 = CW(90), 0 = unsupported/not perpendicular.
    /// </summary>
    public int Rot90Sign()
    {
        // Must be perpendicular (dot product is 0).
        if (entryDir.x * exitDir.x + entryDir.y * exitDir.y != 0)
            return 0;

        // 2D cross product (z component).
        int cross = entryDir.x * exitDir.y - entryDir.y * exitDir.x;

        if (cross > 0) return +1; // Counter-clockwise (CCW).
        if (cross < 0) return -1; // Clockwise (CW).
        return 0;
    }

    public void SnapToGrid()
    {
        if (grid == null) return;
        Vector3 w = grid.GridToWorld(x, y);
        transform.position = new Vector3(w.x, transform.position.y, w.z);
    }

    // Replicator forward direction is defined by exitDir.
    public Vector2Int GetDir() => exitDir;

    public void SetDir(Vector2Int exit)
    {
        if (exit == Vector2Int.zero) return;

        // Use exitDir as facing; entryDir is fixed to CW 90 deg of exitDir.
        exitDir = exit;
        entryDir = new Vector2Int(exit.y, -exit.x); // CW 90: (x,y)->(y,-x)
    }

    public void SetPorts(Vector2Int entry, Vector2Int exit)
    {
        if (entry == Vector2Int.zero || exit == Vector2Int.zero) return;

        // Must be perpendicular.
        if (entry.x * exit.x + entry.y * exit.y != 0)
        {
            Debug.LogWarning($"Replicator ports must be perpendicular: entry={entry}, exit={exit}");
            return;
        }

        entryDir = entry;
        exitDir = exit;
    }
}
