using System.Collections;
using UnityEngine;

public class PlayerMover : MonoBehaviour
{
    public GridManager2D grid;
    public float moveTime = 0.12f; // 一步多久
    public int x;
    public int y;

    [Header("Hold-to-move")]
    public bool holdToMove = true;
    public float repeatDelay = 0.22f;    // 按住后，第一次重复前的延迟
    public float repeatInterval = 0.10f; // 重复频率（越小越快）

    private Vector2Int heldDir = Vector2Int.zero;
    private float holdTimer = 0f;
    private bool didFirstHoldStep = false;

    public float footYOffset = 0.5f; // 角色脚底离逻辑平面的高度
    public bool autoFootFromCollider = true;

    [Header("Model Facing")]
    public float modelYawOffset = 0f;

    private bool moving;

    private void Start()
    {
        if (grid == null) grid = FindObjectOfType<GridManager2D>();

        // 自动用 Collider 的 bounds 来算脚底偏移（推荐）
        if (autoFootFromCollider)
        {
            var col = GetComponentInChildren<Collider>();
            if (col != null)
            {
                // 让 collider 的底部刚好贴在 grid.tileTopY 上
                float bottomToPivot = transform.position.y - col.bounds.min.y;
                footYOffset = bottomToPivot;
            }
        }

        Vector3 p = transform.position;
        x = Mathf.RoundToInt(p.x / grid.cellSize);
        y = Mathf.RoundToInt(p.z / grid.cellSize);

        var world = grid.GridToWorld(x, y);
        transform.position = new Vector3(world.x, grid.tileTopY + footYOffset, world.z);
    }

    private void Update()
    {
        if (grid == null) return;
        if (StepManager.I == null) return;

        // 先读取“瞬时按下”（首步立即走）
        Vector2Int downDir = ReadDownDir();
        if (downDir != Vector2Int.zero)
        {
            heldDir = downDir;
            holdTimer = 0f;
            didFirstHoldStep = false;

            TryStep(heldDir); // 首步立即触发
            return;
        }

        if (!holdToMove)
            return;

        // 再读取“按住方向”
        Vector2Int holdDirNow = ReadHoldDir();
        if (holdDirNow == Vector2Int.zero)
        {
            // 松开：清空状态
            heldDir = Vector2Int.zero;
            holdTimer = 0f;
            didFirstHoldStep = false;
            return;
        }

        // 方向改变：立刻按新方向走一步
        if (holdDirNow != heldDir)
        {
            heldDir = holdDirNow;
            holdTimer = 0f;
            didFirstHoldStep = false;

            TryStep(heldDir);
            return;
        }

        // 按住同方向：计时重复
        holdTimer += Time.deltaTime;

        // 第一次重复：repeatDelay；之后每 repeatInterval
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
        moving = true;

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

        moving = false;
    }

    private void TryStep(Vector2Int d)
    {
        if (d == Vector2Int.zero) return;

        // ✅ 关键：玩家“尝试”移动就立刻转向（即使撞墙/推不动）
        FaceGridDir(d);

        if (StepManager.I.stepping) return;

        int nx = x + d.x;
        int ny = y + d.y;

        // 撞墙/不可走：不移动，但朝向已更新
        if (!grid.IsWalkable(nx, ny)) return;

        var occ = OccupancyMap.I;
        if (occ != null)
        {
            // 重建占格（保持你原逻辑）
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

            // 目标格是 Auto / Replicator：不能进
            if (blocker is AutoMover) return;
            if (blocker is ReplicatorNode) return;

            // 推箱子
            if (blocker is BoxMover box)
            {
                int bx2 = box.x + d.x;
                int by2 = box.y + d.y;

                if (!grid.IsWalkable(bx2, by2)) return;
                if (occ.Get(bx2, by2) != null) return;

                StepManager.I.RequestStep(() => PushBoxAndStep(box, bx2, by2, nx, ny));
                return;
            }

            // 如果你希望“有其它占格就不能走”，就保留这句
            // if (blocker != null) return;
        }

        StepManager.I.RequestStep(() => StepTo(nx, ny));
    }

    private IEnumerator PushBoxAndStep(BoxMover box, int bx2, int by2, int px2, int py2)
    {
        // 同步开两个协程：箱子和玩家同时移动
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

        Vector3 worldDir;
        if (d == Vector2Int.right) worldDir = Vector3.right;
        else if (d == Vector2Int.left) worldDir = Vector3.left;
        else if (d == Vector2Int.up) worldDir = Vector3.forward;
        else if (d == Vector2Int.down) worldDir = Vector3.back;
        else return;

        transform.rotation = Quaternion.LookRotation(worldDir, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
    }

    private static Vector2Int ReadDownDir()
    {
        // “按下”优先：首步立即响应
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) return Vector2Int.up;
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) return Vector2Int.down;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) return Vector2Int.left;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) return Vector2Int.right;
        return Vector2Int.zero;
    }

    private static Vector2Int ReadHoldDir()
    {
        // 同时按多个方向时，简单按“优先级”取一个
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) return Vector2Int.up;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) return Vector2Int.down;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) return Vector2Int.left;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) return Vector2Int.right;
        return Vector2Int.zero;
    }
}
