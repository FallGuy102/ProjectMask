using System.Collections.Generic;
using UnityEngine;

public class GridManager2D : MonoBehaviour
{
    public float cellSize = 1f;
    public float tileTopY = 0f;

    // level tiles
    public Dictionary<Vector2Int, TileObject> tiles = new();

    private void Awake()
    {
        RebuildFromScene();
    }

    [ContextMenu("Rebuild From Scene")]
    public void RebuildFromScene()
    {
        tiles.Clear();
        var all = FindObjectsOfType<TileObject>(includeInactive: true);

        foreach (var t in all)
        {
            if (t == null) continue;

            var p = t.transform.position;
            int gx = Mathf.RoundToInt(p.x / cellSize);
            int gy = Mathf.RoundToInt(p.z / cellSize);

            t.x = gx;
            t.y = gy;

            tiles[new Vector2Int(gx, gy)] = t;
        }
    }

    public bool HasTile(int x, int y) => tiles.ContainsKey(new Vector2Int(x, y));

    public TileObject GetTile(int x, int y)
    {
        tiles.TryGetValue(new Vector2Int(x, y), out var t);
        return t;
    }

    public Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(x * cellSize, tileTopY, y * cellSize);
    }

    public Vector2Int WorldToGrid(Vector3 world)
    {
        int gx = Mathf.RoundToInt(world.x / cellSize);
        int gy = Mathf.RoundToInt(world.z / cellSize);
        return new Vector2Int(gx, gy);
    }

    public bool IsInside(int x, int y) => HasTile(x, y);

    public bool IsWalkable(int x, int y)
    {
        var t = GetTile(x, y);
        if (t == null) return false;

        if (t.tileType == TileType.Wall) return false;
        if (t.tileType == TileType.Water) return false;
        return true;
    }

    public bool IsGoal(int x, int y)
    {
        var t = GetTile(x, y);
        return t != null && t.tileType == TileType.Goal;
    }

    public bool IsMaskEditDisabled(int x, int y)
    {
        var t = GetTile(x, y);
        if (t == null) return false;
        return t.maskEditDisabled || t.tileType == TileType.MaskDisabled;
    }

    public bool IsLethal(int x, int y)
    {
        var t = GetTile(x, y);
        if (t == null) return false;
        return t.lethal || t.tileType == TileType.Lethal;
    }

    /// <summary>
    /// Dynamic lethal setter used by StepMoveSystem.
    /// Rules:
    /// - Wall tiles are never affected by dynamic lethal.
    /// - Tiles currently occupied by a Box are not affected by dynamic lethal.
    /// - Clearing (on=false) must always apply to avoid leftovers.
    /// </summary>
    public void SetDynamicLethal(int x, int y, bool on)
    {
        var t = GetTile(x, y);
        if (t == null) return;

        // clearing must always run
        if (!on)
        {
            t.SetDynamicLethal(false);
            return;
        }

        // setting: filter
        if (t.tileType == TileType.Wall) return;

        var occ = OccupancyMap.I;
        if (occ != null)
        {
            var blocker = occ.Get(x, y);
            if (blocker is BoxMover) return;
        }

        t.SetDynamicLethal(true);
    }
}
