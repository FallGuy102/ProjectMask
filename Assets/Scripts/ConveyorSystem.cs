using System.Collections.Generic;
using UnityEngine;

public class ConveyorSystem : MonoBehaviour
{
    public GridManager2D grid;

    [Header("Safety")]
    public int maxTeleportsPerStepPerObject = 8; // 防止无限循环（传送到另一个传送带上）

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

        public void TeleportTo(int x, int y) { } // 不会被传送
        public void TrySetFacingDir(Vector2Int d) { }
    }

    private void HandleAfterMove(int step)
    {
        var belts = FindObjectsOfType<ConveyorBelt>();
        if (belts == null || belts.Length == 0) return;

        // 收集可传送对象
        var player = FindObjectOfType<PlayerMover>();
        var autos = FindObjectsOfType<AutoMover>();

        // 你的工程里箱子脚本可能叫 BoxMover（你之前做过），也可能是别的名字。
        // 这里用 GetComponentsInScene 的方式，避免你改名后要改系统。
        var boxes = FindObjectsOfType<BoxMover>();       // 如果你叫 BoxMover

        // 全部放到一个列表里按顺序处理：玩家→箱子→auto→传送带→L
        // 这样比较符合“玩家优先”的直觉
        var teleList = new List<IConveyable>();

        if (player != null && player.gameObject.activeSelf)
            teleList.Add(new ConveyablePlayer(player));

        foreach (var b in boxes)
            if (b != null && b.gameObject.activeSelf)
                teleList.Add(new ConveyableBox(b));

        foreach (var a in autos)
            if (a != null && a.gameObject.activeSelf)
                teleList.Add(new ConveyableAuto(a));

        //foreach (var belt in belts)
        //    if (belt != null && belt.gameObject.activeSelf)
        //        teleList.Add(new ConveyableBelt(belt));

        // 迭代处理：可能会“传送到另一条传送带上”，所以允许多次，但有限制
        // 同时用占格避免把人传到别人身上
        for (int iter = 0; iter < maxTeleportsPerStepPerObject; iter++)
        {
            bool anyMoved = false;

            // 本轮占格
            var occ = BuildOccupancy(teleList);

            // 对每条传送带按规则处理
            foreach (var belt in belts)
            {
                if (belt == null || !belt.gameObject.activeSelf) continue;

                var start = belt.StartCell;
                var mid = belt.MidCell;
                var end = belt.EndCell;

                // End 必须可走（否则整条带子本轮不工作）
                if (!grid.IsWalkable(end.x, end.y)) continue;

                // ===== 1) 第二格优先：Mid -> End（若 End 被占则 Mid 不动）=====
                occ.TryGetValue(mid, out var midObj);

                if (midObj != null)
                {
                    // End 空才能传
                    if (!occ.ContainsKey(end))
                    {
                        // 从 mid 移除
                        occ.Remove(mid);

                        // 传送到 end（不消耗step；动画由 TeleportTo 自己决定）
                        midObj.TeleportTo(end.x, end.y);

                        // 只有 auto 改朝向（你在 TrySetFacingDir 内已经处理）
                        midObj.TrySetFacingDir(belt.dir);

                        // 占用 end
                        occ[end] = midObj;
                        anyMoved = true;

                        // 更新 midObj 变量：mid 现在空了（供下面 start->mid 使用）
                        midObj = null;
                    }
                    else
                    {
                        // End 被挡：Mid 不动（你的规则）
                    }
                }

                // ===== 2) 第一格：如果 Mid 空，则 Start -> Mid；否则 Start 不动 =====
                if (!occ.ContainsKey(mid)) // Mid 空（注意：可能因为上面 Mid->End 成功而空）
                {
                    occ.TryGetValue(start, out var startObj);
                    if (startObj != null)
                    {
                        // Start->Mid 不需要检查 mid 可走（mid 本来就是 belt 的格子，你应视为可站立）
                        occ.Remove(start);

                        // 这里是“前进到第二格”，不是去 End
                        startObj.TeleportTo(mid.x, mid.y);

                        // auto 同样面向 belt.dir（建议保持一致）
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
            // 如果同格出现，后者覆盖（本来就不该发生）
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
            a.dir = d; // ✅ 只有auto改方向
        }
    }

    // 你的箱子脚本如果叫 BoxMover：按这个写
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

    // 传送带“自己也可被传送”：把它当成一个可移动对象（如果你允许它移动）
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
