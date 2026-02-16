using UnityEngine;

public class ReplicatorNode : MonoBehaviour
{
    public GridManager2D grid;

    [Header("Grid Pos (derived from transform)")]
    public int x;
    public int y;

    [Header("Ports (must be perpendicular)")]
    public Vector2Int entryDir = Vector2Int.down;  // 入口相对复制器的方向（例：下）
    public Vector2Int exitDir = Vector2Int.right; // 出口相对复制器的方向（例：右）

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
    /// 计算把“入口方向”旋到“出口方向”的 90°旋转（只支持垂直）。
    /// 返回：+1 = CCW(逆时针90)，-1 = CW(顺时针90)，0 = 不支持/不垂直
    /// </summary>
    public int Rot90Sign()
    {
        // 必须垂直（点积为 0）
        if (entryDir.x * exitDir.x + entryDir.y * exitDir.y != 0)
            return 0;

        // 2D 叉积（z 分量）
        int cross = entryDir.x * exitDir.y - entryDir.y * exitDir.x;

        if (cross > 0) return +1; // 逆时针 CCW
        if (cross < 0) return -1; // 顺时针 CW
        return 0;
    }

    public void SnapToGrid()
    {
        if (grid == null) return;
        Vector3 w = grid.GridToWorld(x, y);
        transform.position = new Vector3(w.x, transform.position.y, w.z);
    }

    // 复制器的“方向”定义为 exitDir
    public Vector2Int GetDir() => exitDir;

    public void SetDir(Vector2Int exit)
    {
        if (exit == Vector2Int.zero) return;

        // 用 exitDir 表示方向，entryDir 固定为 exitDir 顺时针 90°
        exitDir = exit;
        entryDir = new Vector2Int(exit.y, -exit.x); // CW 90: (x,y)->(y,-x)
    }

    public void SetPorts(Vector2Int entry, Vector2Int exit)
    {
        if (entry == Vector2Int.zero || exit == Vector2Int.zero) return;

        // 必须垂直
        if (entry.x * exit.x + entry.y * exit.y != 0)
        {
            Debug.LogWarning($"Replicator ports must be perpendicular: entry={entry}, exit={exit}");
            return;
        }

        entryDir = entry;
        exitDir = exit;
    }
}
