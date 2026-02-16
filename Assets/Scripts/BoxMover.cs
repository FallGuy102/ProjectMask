using System.Collections;
using UnityEngine;

public class BoxMover : MonoBehaviour
{
    public GridManager2D grid;

    public int x;
    public int y;

    public float moveTime = 0.12f;
    public float yOffset = 1f; // Box bottom height offset. Tune to match your tile height.

    private void Start()
    {
        if (grid == null) grid = FindObjectOfType<GridManager2D>();

        Vector3 p = transform.position;
        x = Mathf.RoundToInt(p.x / grid.cellSize);
        y = Mathf.RoundToInt(p.z / grid.cellSize);

        SnapToGrid();
    }

    public void SnapToGrid()
    {
        Vector3 t = grid.GridToWorld(x, y);
        transform.position = new Vector3(t.x, grid.tileTopY + yOffset, t.z);
    }

    public IEnumerator MoveTo(int nx, int ny)
    {
        // Update logical coordinates first so occupancy changes immediately.
        x = nx;
        y = ny;

        Vector3 from = transform.position;
        Vector3 tpos = grid.GridToWorld(nx, ny);
        Vector3 to = new Vector3(tpos.x, grid.tileTopY + yOffset, tpos.z);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.001f, moveTime);
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
    }
}
