using UnityEngine;

public class ClickMaskCycle : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public GridManager2D grid;

    [Header("Prefabs for Morph")]
    public AutoMover autoPrefab;
    public BoxMover boxPrefab;
    public ConveyorBelt conveyorPrefab;
    public ReplicatorNode replicatorPrefab;

    [Header("Raycast")]
    public LayerMask maskLayer;     // 只射 mask 物体的层（建议新建一层叫 Mask）
    public float rayMax = 200f;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (grid == null) grid = FindObjectOfType<GridManager2D>();
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (cam == null || grid == null) return;

        // 1) 优先点到“物体”
        if (TryPickMaskByCollider(out int gx, out int gy))
        {
            // 玩家不是 mask：玩家格子不响应
            var player = FindObjectOfType<PlayerMover>();
            if (player != null && player.gameObject.activeSelf && player.x == gx && player.y == gy)
                return;

            MaskMorph.CycleAt(grid, gx, gy, autoPrefab, boxPrefab, conveyorPrefab, replicatorPrefab);
            return;
        }

        // 2) 点不到物体再点地板（可选：你也可以直接 return，不允许点空地）
        if (TryPickGridByPlane(out int px, out int py))
        {
            var player = FindObjectOfType<PlayerMover>();
            if (player != null && player.gameObject.activeSelf && player.x == px && player.y == py)
                return;

            // 如果你不想点空地也触发，这里先检查该格是否存在 mask
            // 没 mask 就 return
            if (!HasAnyMaskAt(px, py)) return;

            MaskMorph.CycleAt(grid, px, py, autoPrefab, boxPrefab, conveyorPrefab, replicatorPrefab);
        }
    }

    private bool TryPickMaskByCollider(out int gx, out int gy)
    {
        gx = gy = 0;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayMax, maskLayer))
            return false;

        // 命中哪个类型，就直接取它的 x,y（绝对准确）
        var b = hit.collider.GetComponentInParent<BoxMover>();
        if (b != null) { gx = b.x; gy = b.y; return true; }

        var a = hit.collider.GetComponentInParent<AutoMover>();
        if (a != null) { gx = a.x; gy = a.y; return true; }

        var c = hit.collider.GetComponentInParent<ConveyorBelt>();
        if (c != null) { gx = c.x; gy = c.y; return true; }

        var r = hit.collider.GetComponentInParent<ReplicatorNode>();
        if (r != null) { gx = r.x; gy = r.y; return true; }

        return false;
    }

    private bool TryPickGridByPlane(out int gx, out int gy)
    {
        gx = gy = 0;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        float yPlane = grid.tileTopY;

        if (Mathf.Abs(ray.direction.y) < 1e-6f) return false;
        float t = (yPlane - ray.origin.y) / ray.direction.y;
        if (t < 0f) return false;

        Vector3 hit = ray.origin + ray.direction * t;

        gx = Mathf.RoundToInt(hit.x / grid.cellSize);
        gy = Mathf.RoundToInt(hit.z / grid.cellSize);
        return true;
    }

    private bool HasAnyMaskAt(int x, int y)
    {
        foreach (var b in FindObjectsOfType<BoxMover>())
            if (b && b.gameObject.activeSelf && b.x == x && b.y == y) return true;

        foreach (var a in FindObjectsOfType<AutoMover>())
            if (a && a.gameObject.activeSelf && a.x == x && a.y == y) return true;

        foreach (var c in FindObjectsOfType<ConveyorBelt>())
            if (c && c.gameObject.activeSelf && c.x == x && c.y == y) return true;

        foreach (var r in FindObjectsOfType<ReplicatorNode>())
            if (r && r.gameObject.activeSelf && r.x == x && r.y == y) return true;

        return false;
    }
}
