using System.Collections;
using UnityEngine;

public enum AutoState
{
    Static,
    VerticalMove,
    HorizontalMove
}

public class AutoMover : MonoBehaviour
{
    public GridManager2D grid;

    [Header("Grid Pos")]
    public int x;
    public int y;

    [Header("State")]
    public AutoState state = AutoState.VerticalMove;

    [Header("MOVE Direction (grid)")]
    public Vector2Int dir = Vector2Int.up;

    [Header("Move Feel")]
    public float moveTime = 0.12f;
    public float footYOffset = 0.5f;
    public bool autoFootFromCollider = true;

    [Header("Facing (Model)")]
    public Transform modelRoot;
    public float modelYawOffset = 0f;
    public bool rotateWhenStatic = true;

    private void Start()
    {
        if (grid == null) grid = FindObjectOfType<GridManager2D>();

        if (autoFootFromCollider)
        {
            var col = GetComponentInChildren<Collider>();
            if (col != null)
            {
                float bottomToPivot = transform.position.y - col.bounds.min.y;
                footYOffset = bottomToPivot;
            }
        }

        Vector3 p = transform.position;
        x = Mathf.RoundToInt(p.x / grid.cellSize);
        y = Mathf.RoundToInt(p.z / grid.cellSize);

        SanitizeDirByState();
        SnapToGrid();
        ApplyFacing();

        if (StepManager.I != null)
            StepManager.I.OnStepBegin += OnStepBegin;
    }

    private void OnDestroy()
    {
        if (StepManager.I != null)
            StepManager.I.OnStepBegin -= OnStepBegin;
    }

    private void OnStepBegin(int step)
    {
        StepMoveSystem.QueueMove(this);
    }

    public void SnapToGrid()
    {
        Vector3 t = grid.GridToWorld(x, y);
        transform.position = new Vector3(t.x, grid.tileTopY + footYOffset, t.z);
    }

    public void SetDirImmediate(Vector2Int newDir)
    {
        if (newDir == Vector2Int.zero) return;
        dir = newDir;
        SanitizeDirByState();
        ApplyFacing();
    }

    public void SetStateAndDir(AutoState newState, Vector2Int initialDir)
    {
        state = newState;
        dir = initialDir;
        SanitizeDirByState();
        ApplyFacing();
    }

    private void SanitizeDirByState()
    {
        if (dir == Vector2Int.zero) dir = Vector2Int.up;

        if (state == AutoState.VerticalMove)
        {
            if (dir != Vector2Int.up && dir != Vector2Int.down)
                dir = (dir.y < 0) ? Vector2Int.down : Vector2Int.up;
        }
        else if (state == AutoState.HorizontalMove)
        {
            if (dir != Vector2Int.left && dir != Vector2Int.right)
                dir = (dir.x < 0) ? Vector2Int.left : Vector2Int.right;
        }
    }

    private void ApplyFacing()
    {
        if (!rotateWhenStatic && state == AutoState.Static) return;

        Vector2Int d = dir;
        if (d == Vector2Int.zero) return;

        Vector3 worldForward =
            (d == Vector2Int.right) ? Vector3.right :
            (d == Vector2Int.left) ? Vector3.left :
            (d == Vector2Int.up) ? Vector3.forward :
            (d == Vector2Int.down) ? Vector3.back :
            Vector3.forward;

        Transform t = (modelRoot != null) ? modelRoot : transform;
        t.rotation = Quaternion.LookRotation(worldForward, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
    }

    public IEnumerator MoveOneStep()
    {
        if (state == AutoState.Static)
        {
            ApplyFacing();
            yield break;
        }

        SanitizeDirByState();
        ApplyFacing();

        int nx = x + dir.x;
        int ny = y + dir.y;

        if (!grid.IsWalkable(nx, ny))
        {
            SetDirImmediate(-dir);
            yield break;
        }

        var occ = OccupancyMap.I;
        if (occ != null)
        {
            var blocker = occ.Get(nx, ny);

            if (blocker is PlayerMover || blocker is AutoMover || blocker is ReplicatorNode)
            {
                SetDirImmediate(-dir);
                yield break;
            }

            if (blocker is BoxMover box)
            {
                int bx2 = box.x + dir.x;
                int by2 = box.y + dir.y;

                if (!grid.IsWalkable(bx2, by2) || occ.Get(bx2, by2) != null)
                {
                    SetDirImmediate(-dir);
                    yield break;
                }

                yield return PushBoxAndMoveAuto(box, bx2, by2, nx, ny);
                yield break;
            }
        }

        Vector3 from = transform.position;
        Vector3 tpos = grid.GridToWorld(nx, ny);
        Vector3 to = new Vector3(tpos.x, transform.position.y, tpos.z);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.001f, moveTime);
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }

        x = nx;
        y = ny;
    }

    private IEnumerator PushBoxAndMoveAuto(BoxMover box, int bx2, int by2, int ax2, int ay2)
    {
        bool boxDone = false;
        bool autoDone = false;

        StartCoroutine(Run(box.MoveTo(bx2, by2), () => boxDone = true));
        StartCoroutine(Run(MoveSelfTo(ax2, ay2), () => autoDone = true));

        yield return new WaitUntil(() => boxDone && autoDone);

        x = ax2;
        y = ay2;
    }

    private IEnumerator MoveSelfTo(int nx, int ny)
    {
        Vector3 from = transform.position;
        Vector3 tpos = grid.GridToWorld(nx, ny);
        Vector3 to = new Vector3(tpos.x, transform.position.y, tpos.z);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.001f, moveTime);
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
    }

    private IEnumerator Run(IEnumerator routine, System.Action onDone)
    {
        yield return routine;
        onDone?.Invoke();
    }

    // =====================
    // Dynamic lethal
    // =====================
    public void ApplyLethal(GridManager2D g)
    {
        if (g == null) return;

        if (state == AutoState.Static)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    MarkLethalIfExists(g, x + dx, y + dy);
            return;
        }

        if (state == AutoState.VerticalMove)
        {
            SanitizeDirByState();
            MarkLethalIfExists(g, x, y);
            MarkLethalIfExists(g, x + dir.x, y + dir.y);
            return;
        }

        // HorizontalMove: up-facing 3x2 area.
        // rows: y and y+1 (2 rows)
        // cols: x-1, x, x+1 (3 cols)
        for (int dy = 0; dy <= 1; dy++)          // y, y+1
            for (int dx = -1; dx <= 1; dx++)         // x-1, x, x+1
            {
                int gx = x + dx;
                int gy = y + dy;
                MarkLethalIfExists(g, gx, gy);
            }
    }

    private static void MarkLethalIfExists(GridManager2D g, int gx, int gy)
    {
        if (g.GetTile(gx, gy) == null) return;
        g.SetDynamicLethal(gx, gy, true);
    }
}
