using System.Collections;
using UnityEngine;

public class PlayerMover : MonoBehaviour
{
    public GridManager2D grid;
    public float moveTime = 0.10f;

    [Header("Animation Timing")]
    public float animationStepDuration = 0.18f;

    public int x;
    public int y;

    [Header("Hold-to-move")]
    public bool holdToMove = true;
    public float repeatDelay = 0.16f;
    public float repeatInterval = 0.08f;

    private Vector2Int heldDir = Vector2Int.zero;
    private float holdTimer = 0f;
    private bool didFirstHoldStep = false;

    public float footYOffset = 0.5f;
    public bool autoFootFromCollider = true;

    [Header("Model Facing")]
    public float modelYawOffset = 0f;
    public Vector2Int facingDir = Vector2Int.up;
    private Vector2Int bufferedDir = Vector2Int.zero;

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

        var world = grid.GridToWorld(x, y);
        transform.position = new Vector3(world.x, grid.tileTopY + footYOffset, world.z);

        facingDir = WorldDirToGridDir(transform.forward);
    }

    private void OnDisable()
    {
        ClearInputBuffer();
    }

    public void ClearInputBuffer()
    {
        bufferedDir = Vector2Int.zero;
        heldDir = Vector2Int.zero;
        holdTimer = 0f;
        didFirstHoldStep = false;
    }

    private void Update()
    {
        if (grid == null) return;
        if (StepManager.I == null) return;

        // Consume latest buffered input once the step pipeline is idle.
        if (!StepManager.I.stepping && bufferedDir != Vector2Int.zero)
        {
            var d = bufferedDir;
            bufferedDir = Vector2Int.zero;
            TryStep(d);
            if (StepManager.I.stepping) return;
        }

        Vector2Int downDir = ReadDownDir();
        if (downDir != Vector2Int.zero)
        {
            heldDir = downDir;
            holdTimer = 0f;
            didFirstHoldStep = false;

            TryStep(heldDir);
            return;
        }

        if (!holdToMove)
            return;

        Vector2Int holdDirNow = ReadHoldDir();
        if (holdDirNow == Vector2Int.zero)
        {
            heldDir = Vector2Int.zero;
            holdTimer = 0f;
            didFirstHoldStep = false;
            return;
        }

        if (holdDirNow != heldDir)
        {
            heldDir = holdDirNow;
            holdTimer = 0f;
            didFirstHoldStep = false;

            TryStep(heldDir);
            return;
        }

        holdTimer += Time.deltaTime;

        float threshold = didFirstHoldStep ? repeatInterval : repeatDelay;
        if (holdTimer >= threshold)
        {
            holdTimer = 0f;
            didFirstHoldStep = true;

            TryStep(heldDir);
        }
    }

    private IEnumerator StepTo(int nx, int ny)
    {

        Vector3 from = transform.position;
        Vector3 tpos = grid.GridToWorld(nx, ny);
        Vector3 to = new Vector3(tpos.x, grid.tileTopY + footYOffset, tpos.z);

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

    private void TryStep(Vector2Int d)
    {
        if (d == Vector2Int.zero) return;

        // Keep only the latest intent while stepping. Avoid queued zig-zag paths.
        if (StepManager.I.stepping)
        {
            bufferedDir = d;
            return;
        }

        FaceGridDir(d);

        int nx = x + d.x;
        int ny = y + d.y;

        if (!grid.IsWalkable(nx, ny)) return;

        var occ = OccupancyMap.I;
        if (occ != null)
        {
            occ.Clear();
            occ.Set(x, y, this);

            foreach (var a in FindObjectsOfType<AutoMover>())
                if (a != null && a.gameObject.activeSelf)
                    occ.Set(a.x, a.y, a);

            foreach (var b in FindObjectsOfType<BoxMover>())
                if (b != null && b.gameObject.activeSelf)
                    occ.Set(b.x, b.y, b);

            foreach (var r in FindObjectsOfType<ReplicatorNode>())
                if (r != null && r.gameObject.activeSelf)
                    occ.Set(r.x, r.y, r);

            var blocker = occ.Get(nx, ny);

            if (blocker is AutoMover) return;
            if (blocker is ReplicatorNode) return;

            if (blocker is BoxMover box)
            {
                int bx2 = box.x + d.x;
                int by2 = box.y + d.y;

                if (!grid.IsWalkable(bx2, by2)) return;
                if (occ.Get(bx2, by2) != null) return;

                StepManager.I.RequestStep(() => PushBoxAndStep(box, bx2, by2, nx, ny));
                return;
            }
        }

        StepManager.I.RequestStep(() => StepTo(nx, ny));
    }

    private IEnumerator PushBoxAndStep(BoxMover box, int bx2, int by2, int px2, int py2)
    {
        bool boxDone = false;
        bool playerDone = false;

        StartCoroutine(Run(box.MoveTo(bx2, by2), () => boxDone = true));
        StartCoroutine(Run(StepTo(px2, py2), () => playerDone = true));

        yield return new WaitUntil(() => boxDone && playerDone);
    }

    private IEnumerator Run(IEnumerator routine, System.Action onDone)
    {
        yield return routine;
        onDone?.Invoke();
    }

    private void FaceGridDir(Vector2Int d)
    {
        if (d == Vector2Int.zero) return;

        facingDir = d;

        Vector3 worldDir;
        if (d == Vector2Int.right) worldDir = Vector3.right;
        else if (d == Vector2Int.left) worldDir = Vector3.left;
        else if (d == Vector2Int.up) worldDir = Vector3.forward;
        else if (d == Vector2Int.down) worldDir = Vector3.back;
        else return;

        transform.rotation = Quaternion.LookRotation(worldDir, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
    }

    private static Vector2Int WorldDirToGridDir(Vector3 forward)
    {
        if (Mathf.Abs(forward.x) >= Mathf.Abs(forward.z))
            return forward.x >= 0 ? Vector2Int.right : Vector2Int.left;

        return forward.z >= 0 ? Vector2Int.up : Vector2Int.down;
    }

    private static Vector2Int ReadDownDir()
    {
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) return Vector2Int.up;
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) return Vector2Int.down;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) return Vector2Int.left;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) return Vector2Int.right;
        return Vector2Int.zero;
    }

    private static Vector2Int ReadHoldDir()
    {
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) return Vector2Int.up;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) return Vector2Int.down;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) return Vector2Int.left;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) return Vector2Int.right;
        return Vector2Int.zero;
    }
}

