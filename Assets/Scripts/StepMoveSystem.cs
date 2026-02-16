using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class StepMoveSystem : MonoBehaviour
{
    private static StepMoveSystem _i;
    public static StepMoveSystem I => _i;

    private readonly List<AutoMover> queued = new();

    private GridManager2D _grid;
    private readonly HashSet<Vector2Int> _prevDynamic = new();

    private void Awake() { _i = this; }

    public static void QueueMove(AutoMover mover)
    {
        if (I == null) return;
        if (!I.queued.Contains(mover))
            I.queued.Add(mover);
    }

    public IEnumerator ExecuteQueuedMoves()
    {
        if (_grid == null) _grid = FindObjectOfType<GridManager2D>();

        RebuildOccupancyAll();
        if (queued.Count == 0) yield break;

        var targetOf = new Dictionary<AutoMover, Vector2Int>();
        var wantsReverse = new HashSet<AutoMover>();

        foreach (var a in queued)
        {
            if (a == null || !a.gameObject.activeSelf) continue;
            if (a.state == AutoState.Static) continue;

            int nx = a.x + a.dir.x;
            int ny = a.y + a.dir.y;

            if (!a.grid.IsWalkable(nx, ny))
            {
                wantsReverse.Add(a);
                continue;
            }

            targetOf[a] = new Vector2Int(nx, ny);
        }

        // occupancy blocking with lethal priority
        var occ = OccupancyMap.I;
        if (occ != null)
        {
            foreach (var kv in targetOf)
            {
                var a = kv.Key;
                if (a == null || !a.gameObject.activeSelf) continue;
                if (wantsReverse.Contains(a)) continue;

                var t = kv.Value;
                var blocker = occ.Get(t.x, t.y);

                if (blocker is PlayerMover p)
                {
                    if (WouldLethalHitCell(a, p.x, p.y))
                    {
                        KillPlayer(p);
                        RebuildOccupancyAll();
                        continue;
                    }

                    wantsReverse.Add(a);
                    continue;
                }

                if (blocker is AutoMover || blocker is ReplicatorNode)
                {
                    wantsReverse.Add(a);
                    continue;
                }
            }
        }

        // same target => reverse
        var byTarget = new Dictionary<Vector2Int, List<AutoMover>>();
        foreach (var kv in targetOf)
        {
            var a = kv.Key;
            if (a == null || !a.gameObject.activeSelf) continue;
            if (wantsReverse.Contains(a)) continue;

            var t = kv.Value;
            if (!byTarget.TryGetValue(t, out var list))
            {
                list = new List<AutoMover>();
                byTarget[t] = list;
            }
            list.Add(a);
        }

        foreach (var kv in byTarget)
            if (kv.Value.Count >= 2)
                foreach (var a in kv.Value) wantsReverse.Add(a);

        // head-on swap => reverse
        var autos = new List<AutoMover>(targetOf.Keys);
        for (int i = 0; i < autos.Count; i++)
        {
            var a = autos[i];
            if (a == null || !a.gameObject.activeSelf) continue;
            if (wantsReverse.Contains(a)) continue;
            if (!targetOf.TryGetValue(a, out var at)) continue;

            for (int j = i + 1; j < autos.Count; j++)
            {
                var b = autos[j];
                if (b == null || !b.gameObject.activeSelf) continue;
                if (wantsReverse.Contains(b)) continue;
                if (!targetOf.TryGetValue(b, out var bt)) continue;

                if (at.x == b.x && at.y == b.y && bt.x == a.x && bt.y == a.y)
                {
                    wantsReverse.Add(a);
                    wantsReverse.Add(b);
                }
            }
        }

        // reverse first
        foreach (var a in wantsReverse)
            if (a != null && a.gameObject.activeSelf)
                a.SetDirImmediate(-a.dir);

        // lethal before move
        RebuildDynamicLethalIncremental();
        yield return null; // render color first

        // move in parallel
        int pending = 0;
        foreach (var kv in targetOf)
        {
            var a = kv.Key;
            if (a == null || !a.gameObject.activeSelf) continue;
            if (wantsReverse.Contains(a)) continue;

            pending++;
            StartCoroutine(RunAndCount(a, () => pending--));
        }

        yield return new WaitUntil(() => pending == 0);

        RebuildOccupancyAll();

        // lethal after move
        RebuildDynamicLethalIncremental();
        yield return null;

        queued.Clear();
    }

    private IEnumerator RunAndCount(AutoMover m, System.Action onDone)
    {
        yield return m.MoveOneStep();
        onDone?.Invoke();
    }

    private void RebuildOccupancyAll()
    {
        var occ = OccupancyMap.I;
        if (occ == null) return;

        occ.Clear();

        var player = FindObjectOfType<PlayerMover>(includeInactive: true);
        if (player != null && player.gameObject.activeSelf)
            occ.Set(player.x, player.y, player);

        foreach (var a in FindObjectsOfType<AutoMover>())
            if (a != null && a.gameObject.activeSelf)
                occ.Set(a.x, a.y, a);

        foreach (var b in FindObjectsOfType<BoxMover>())
            if (b != null && b.gameObject.activeSelf)
                occ.Set(b.x, b.y, b);

        foreach (var r in FindObjectsOfType<ReplicatorNode>())
            if (r != null && r.gameObject.activeSelf)
                occ.Set(r.x, r.y, r);
    }

    private void RebuildDynamicLethalIncremental()
    {
        if (_grid == null) _grid = FindObjectOfType<GridManager2D>();
        if (_grid == null) return;

        foreach (var p in _prevDynamic)
            _grid.SetDynamicLethal(p.x, p.y, false);
        _prevDynamic.Clear();

        var allAutos = FindObjectsOfType<AutoMover>();
        foreach (var a in allAutos)
        {
            if (a == null || !a.gameObject.activeSelf) continue;

            a.ApplyLethal(_grid);

            if (a.state == AutoState.Static)
            {
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        _prevDynamic.Add(new Vector2Int(a.x + dx, a.y + dy));
            }
            else if (a.state == AutoState.VerticalMove)
            {
                _prevDynamic.Add(new Vector2Int(a.x, a.y));
                _prevDynamic.Add(new Vector2Int(a.x + a.dir.x, a.y + a.dir.y));
            }
            else
            {
                // ✅ HorizontalMove: up-facing 3x2 (y and y+1, x-1..x+1)
                for (int dy = 0; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        _prevDynamic.Add(new Vector2Int(a.x + dx, a.y + dy));
            }
        }
    }

    public void ForceRebuildDynamicLethalFull()
    {
        if (_grid == null) _grid = FindObjectOfType<GridManager2D>();
        if (_grid == null) return;

        foreach (var kv in _grid.tiles)
        {
            var tile = kv.Value;
            if (tile != null) tile.SetDynamicLethal(false);
        }

        _prevDynamic.Clear();

        var allAutos = FindObjectsOfType<AutoMover>();
        foreach (var a in allAutos)
        {
            if (a == null || !a.gameObject.activeSelf) continue;

            a.ApplyLethal(_grid);

            if (a.state == AutoState.Static)
            {
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        _prevDynamic.Add(new Vector2Int(a.x + dx, a.y + dy));
            }
            else if (a.state == AutoState.VerticalMove)
            {
                _prevDynamic.Add(new Vector2Int(a.x, a.y));
                _prevDynamic.Add(new Vector2Int(a.x + a.dir.x, a.y + a.dir.y));
            }
            else
            {
                for (int dy = 0; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        _prevDynamic.Add(new Vector2Int(a.x + dx, a.y + dy));
            }
        }
    }

    private static bool WouldLethalHitCell(AutoMover a, int cellX, int cellY)
    {
        if (a == null) return false;

        if (a.state == AutoState.Static)
            return Mathf.Abs(cellX - a.x) <= 1 && Mathf.Abs(cellY - a.y) <= 1;

        if (a.state == AutoState.VerticalMove)
        {
            if (cellX == a.x && cellY == a.y) return true;
            if (cellX == a.x + a.dir.x && cellY == a.y + a.dir.y) return true;
            return false;
        }

        // ✅ HorizontalMove: up-facing 3x2 (x-1..x+1, y..y+1)
        bool inX = (cellX >= a.x - 1 && cellX <= a.x + 1);
        bool inY = (cellY >= a.y && cellY <= a.y + 1);
        return inX && inY;
    }

    private static void KillPlayer(PlayerMover p)
    {
        if (p == null) return;

        var t = p.GetType();
        var die = t.GetMethod("Die", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
               ?? t.GetMethod("Kill", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
               ?? t.GetMethod("OnDeath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (die != null && die.GetParameters().Length == 0)
        {
            die.Invoke(p, null);
            return;
        }

        p.gameObject.SetActive(false);
    }
}
