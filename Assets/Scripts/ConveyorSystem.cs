using System.Collections.Generic;
using UnityEngine;

public class ConveyorSystem : MonoBehaviour
{
    public GridManager2D grid;

    [Header("Safety")]
    public int maxTeleportsPerStepPerObject = 8; // Safety cap to prevent infinite teleport loops (e.g. belt-to-belt chaining).

    private void Start()
    {
        if (grid == null) grid = FindObjectOfType<GridManager2D>();
        StepManager.I.OnStepAfterMove += HandleAfterMove;
    }

    private void OnDestroy()
    {
        if (StepManager.I != null)
            StepManager.I.OnStepAfterMove -= HandleAfterMove;
    }

    private class StaticBlocker : IConveyable
    {
        public int X { get; }
        public int Y { get; }
        public bool IsAlive => true;

        public StaticBlocker(int x, int y) { X = x; Y = y; }

        public void TeleportTo(int x, int y) { } // This blocker type cannot be teleported.
        public void TrySetFacingDir(Vector2Int d) { }
    }

    private void HandleAfterMove(int step)
    {
        var belts = FindObjectsOfType<ConveyorBelt>();
        if (belts == null || belts.Length == 0) return;

        // Collect objects that can be conveyed.
        var player = FindObjectOfType<PlayerMover>();
        var autos = FindObjectsOfType<AutoMover>();

        // Box script may be named BoxMover or something else depending on project history.
        // Use scene scanning to avoid coupling to specific rename history.
        var boxes = FindObjectsOfType<BoxMover>();       // If your box script is BoxMover, this line is correct.

        // Process in order: player -> box -> auto -> belt -> static blocker.
        // This ordering better matches player-first expectations.
        var teleList = new List<IConveyable>();

        if (player != null && player.gameObject.activeSelf)
            teleList.Add(new ConveyablePlayer(player));

        foreach (var b in boxes)
            if (b != null && b.gameObject.activeSelf)
                teleList.Add(new ConveyableBox(b));

        foreach (var a in autos)
            if (a != null && a.gameObject.activeSelf)
                teleList.Add(new ConveyableAuto(a));

        // Apply rules to each conveyor belt.
        //    if (belt != null && belt.gameObject.activeSelf)
        //        teleList.Add(new ConveyableBelt(belt));

        // Iterative pass: objects can land on another belt in the same step.
        // Occupancy map prevents teleporting onto an occupied cell.
        for (int iter = 0; iter < maxTeleportsPerStepPerObject; iter++)
        {
            bool anyMoved = false;

            // Occupancy for this iteration.
            var occ = BuildOccupancy(teleList);

            // Process each conveyor belt by rules.
            foreach (var belt in belts)
            {
                if (belt == null || !belt.gameObject.activeSelf) continue;

                var start = belt.StartCell;
                var mid = belt.MidCell;
                var end = belt.EndCell;

                // End cell must be walkable, otherwise this belt does not work this iteration.
                if (!grid.IsWalkable(end.x, end.y)) continue;

                // 1) Mid -> End first; if End is blocked, Mid stays.
                occ.TryGetValue(mid, out var midObj);

                if (midObj != null)
                {
                    // Move only if End is empty.
                    if (!occ.ContainsKey(end))
                    {
                        // Remove from Mid.
                        occ.Remove(mid);

                        // Teleport to End (no extra step cost; animation behavior is defined by TeleportTo).
                        midObj.TeleportTo(end.x, end.y);

                        // Only Auto changes facing here (handled in TrySetFacingDir).
                        midObj.TrySetFacingDir(belt.dir);

                        // Occupy End.
                        occ[end] = midObj;
                        anyMoved = true;

                        // Update local midObj state: Mid is now empty for Start -> Mid logic.
                        midObj = null;
                    }
                    else
                    {
                        // End is blocked: Mid stays in place (per design).
                    }
                }

                // 2) Start -> Mid only when Mid is empty.
                if (!occ.ContainsKey(mid)) // Mid is empty (possibly emptied by Mid -> End above).
                {
                    occ.TryGetValue(start, out var startObj);
                    if (startObj != null)
                    {
                        // No extra walkable check needed for Mid; it is a belt tile.
                        occ.Remove(start);

                        // This moves to Mid (second cell), not directly to End.
                        startObj.TeleportTo(mid.x, mid.y);

                        // Auto also faces belt.dir for consistency.
                        startObj.TrySetFacingDir(belt.dir);

                        occ[mid] = startObj;
                        anyMoved = true;
                    }
                }
            }

            if (!anyMoved) break;
        }
    }

    // ===== Occupancy =====
    private Dictionary<Vector2Int, IConveyable> BuildOccupancy(List<IConveyable> list)
    {
        var occ = new Dictionary<Vector2Int, IConveyable>();
        foreach (var o in list)
        {
            if (!o.IsAlive) continue;
            var key = new Vector2Int(o.X, o.Y);
            // If duplicate occupancy appears, later write wins (should not normally happen).
            occ[key] = o;
        }

        foreach (var r in FindObjectsOfType<ReplicatorNode>())
        {
            if (r == null || !r.gameObject.activeSelf) continue;
            occ[new Vector2Int(r.x, r.y)] = new StaticBlocker(r.x, r.y);
        }

        return occ;
    }

    // ===== Adapter layer: avoid rewriting your existing classes =====
    private interface IConveyable
    {
        int X { get; }
        int Y { get; }
        bool IsAlive { get; }
        void TeleportTo(int x, int y);
        void TrySetFacingDir(Vector2Int d); // default: do nothing
    }

    private class ConveyablePlayer : IConveyable
    {
        private readonly PlayerMover p;
        public ConveyablePlayer(PlayerMover p) { this.p = p; }
        public int X => p.x; public int Y => p.y;
        public bool IsAlive => p != null && p.gameObject.activeSelf;

        public void TeleportTo(int x, int y)
        {
            p.x = x; p.y = y;
            var w = p.grid.GridToWorld(x, y);

            Vector3 target = new Vector3(w.x, p.grid.tileTopY + p.footYOffset, w.z);

            var anim = p.GetComponent<TeleportAnimator>();
            if (anim == null) anim = p.gameObject.AddComponent<TeleportAnimator>();
            anim.PlayTo(target, 0.06f);
        }

        public void TrySetFacingDir(Vector2Int d) { }
    }

    private class ConveyableAuto : IConveyable
    {
        private readonly AutoMover a;
        public ConveyableAuto(AutoMover a) { this.a = a; }
        public int X => a.x; public int Y => a.y;
        public bool IsAlive => a != null && a.gameObject.activeSelf;

        public void TeleportTo(int x, int y)
        {
            a.x = x; a.y = y;
            var w = a.grid.GridToWorld(x, y);

            Vector3 target = new Vector3(w.x, a.grid.tileTopY + a.footYOffset, w.z);

            var anim = a.GetComponent<TeleportAnimator>();
            if (anim == null) anim = a.gameObject.AddComponent<TeleportAnimator>();
            anim.PlayTo(target, 0.06f);
        }

        public void TrySetFacingDir(Vector2Int d)
        {
            a.dir = d; // Only Auto changes direction.
        }
    }

    // If your box script is BoxMover, keep this implementation.
    private class ConveyableBox : IConveyable
    {
        private readonly BoxMover b;
        public ConveyableBox(BoxMover b) { this.b = b; }
        public int X => b.x; public int Y => b.y;
        public bool IsAlive => b != null && b.gameObject.activeSelf;

        public void TeleportTo(int x, int y)
        {
            b.x = x; b.y = y;
            var w = b.grid.GridToWorld(x, y);

            Vector3 target = new Vector3(w.x, b.grid.tileTopY + b.yOffset, w.z);

            var anim = b.GetComponent<TeleportAnimator>();
            if (anim == null) anim = b.gameObject.AddComponent<TeleportAnimator>();
            anim.PlayTo(target, 0.06f);
        }

        public void TrySetFacingDir(Vector2Int d) { }
    }

    // Conveyor itself can be conveyable if your design allows belt movement.
    private class ConveyableBelt : IConveyable
    {
        private readonly ConveyorBelt belt;
        public ConveyableBelt(ConveyorBelt b) { belt = b; }
        public int X => belt.x; public int Y => belt.y;
        public bool IsAlive => belt != null && belt.gameObject.activeSelf;

        public void TeleportTo(int x, int y)
        {
            belt.x = x; belt.y = y;
            var w = belt.grid.GridToWorld(x, y);
            belt.transform.position = new Vector3(w.x, belt.transform.position.y, w.z);
        }

        public void TrySetFacingDir(Vector2Int d) { }
    }
}
