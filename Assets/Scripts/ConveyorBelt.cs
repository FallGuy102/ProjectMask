using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    public GridManager2D grid;

    public int x;
    public int y;

    public Vector2Int dir = Vector2Int.right;

    public Vector2Int StartCell => new Vector2Int(x, y);
    public Vector2Int MidCell => new Vector2Int(x + dir.x, y + dir.y);
    public Vector2Int EndCell => new Vector2Int(x + dir.x * 2, y + dir.y * 2);

    private void Start()
    {
        if (grid == null)
            grid = FindObjectOfType<GridManager2D>();

        SyncFromWorld();
    }

    public void SyncFromWorld()
    {
        Vector3 p = transform.position;
        x = Mathf.RoundToInt(p.x / grid.cellSize);
        y = Mathf.RoundToInt(p.z / grid.cellSize);
    }

    public bool Covers(int cx, int cy)
    {
        return (cx == StartCell.x && cy == StartCell.y)
            || (cx == MidCell.x && cy == MidCell.y);
    }

    public void SnapToGrid()
    {
        if (grid == null) return;
        Vector3 w = grid.GridToWorld(x, y);
        transform.position = new Vector3(w.x, transform.position.y, w.z);
    }

    public Vector2Int GetDir() => dir;

    public void SetDir(Vector2Int d)
    {
        if (d == Vector2Int.zero) return;
        dir = d;
    }
}