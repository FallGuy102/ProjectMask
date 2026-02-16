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
    public LayerMask maskLayer;     // Layer that includes only mask objects (recommended: a dedicated Mask layer).
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

        // 1) Prefer clicking an object first.
        if (TryPickMaskByCollider(out int gx, out int gy))
        {
            // Player is not a mask target: ignore player cell.
            var player = FindObjectOfType<PlayerMover>();
            if (player != null && player.gameObject.activeSelf && player.x == gx && player.y == gy)
                return;

            MaskMorph.CycleAt(grid, gx, gy, autoPrefab, boxPrefab, conveyorPrefab, replicatorPrefab);
            return;
        }

        // 2) If no object is hit, fallback to floor click (optional behavior).
        if (TryPickGridByPlane(out int px, out int py))
        {
            var player = FindObjectOfType<PlayerMover>();
            if (player != null && player.gameObject.activeSelf && player.x == px && player.y == py)
                return;

            // If empty-floor clicks should not trigger, check whether this cell has a mask first.
            // No mask on this cell: return early.
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

        // Read x/y directly from the hit object type for exact coordinates.
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
