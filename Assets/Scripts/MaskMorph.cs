using UnityEngine;

public enum MaskType
{
    Box,
    Conveyor,
    Replicator,
    Auto,
}

public static class MaskMorph
{
    /// <summary>
    /// 把目标格子上的“mask物体”切换成指定类型。
    /// - 支持 Box / Auto / Conveyor / Replicator 四种互转
    /// - dirOverride：给 Auto/Conveyor 的方向覆写（Box忽略）
    /// - Replicator 推荐使用 repEntryOverride + repExitOverride（来自画线两段）
    /// - 未提供 dirOverride 时：尽量继承旧物体方向；如果旧物体无方向（Box），用 Vector2Int.right
    /// </summary>
    public static void MorphAt(
        GridManager2D grid,
        int x, int y,
        MaskType toType,
        Vector2Int? dirOverride = null,
        Vector2Int? repEntryOverride = null,
        Vector2Int? repExitOverride = null,
        AutoMover autoPrefab = null,
        BoxMover boxPrefab = null,
        ConveyorBelt conveyorPrefab = null,
        ReplicatorNode replicatorPrefab = null
    )
    {
        if (grid == null) { Debug.LogError("MorphAt: grid is null"); return; }

        // 找到此格子的现有对象（四种mask）
        var oldBox = FindAt<BoxMover>(x, y);
        var oldAuto = FindAt<AutoMover>(x, y);
        var oldConv = FindAt<ConveyorBelt>(x, y);
        var oldRep = FindAt<ReplicatorNode>(x, y);

        // 没有mask就不处理
        if (oldBox == null && oldAuto == null && oldConv == null && oldRep == null)
            return;

        // 继承方向：优先用 dirOverride；否则从旧物体读取；Box 无方向就用默认
        Vector2Int inheritedDir = dirOverride ?? GetDirFromOld(oldAuto, oldConv, oldRep, Vector2Int.right);
        inheritedDir = SafeDir(inheritedDir);

        // 如果目标类型和当前类型一致：只做“方向更新 + 对齐”
        var curType = GetCurrentType(oldBox, oldAuto, oldConv, oldRep);
        if (curType == toType)
        {
            ApplyDirIfNeeded(toType, oldAuto, oldConv, oldRep, inheritedDir, repEntryOverride, repExitOverride);
            SnapExisting(grid, oldBox, oldAuto, oldConv, oldRep);
            return;
        }

        // 销毁旧物体（mask 之间互转）
        if (oldBox) Object.Destroy(oldBox.gameObject);
        if (oldAuto) Object.Destroy(oldAuto.gameObject);
        if (oldConv) Object.Destroy(oldConv.gameObject);
        if (oldRep) Object.Destroy(oldRep.gameObject);

        // 生成新物体
        switch (toType)
        {
            case MaskType.Box:
                if (boxPrefab == null) { Debug.LogError("MorphAt: boxPrefab is null"); return; }
                SpawnBox(grid, boxPrefab, x, y);
                break;

            case MaskType.Auto:
                if (autoPrefab == null) { Debug.LogError("MorphAt: autoPrefab is null"); return; }
                SpawnAuto(grid, autoPrefab, x, y, inheritedDir);
                break;

            case MaskType.Conveyor:
                if (conveyorPrefab == null) { Debug.LogError("MorphAt: conveyorPrefab is null"); return; }
                SpawnConveyor(grid, conveyorPrefab, x, y, inheritedDir);
                break;

            case MaskType.Replicator:
                if (replicatorPrefab == null) { Debug.LogError("MorphAt: replicatorPrefab is null"); return; }
                SpawnReplicator(
                    grid, replicatorPrefab, x, y,
                    exitDirFallback: inheritedDir,
                    repEntryOverride: repEntryOverride,
                    repExitOverride: repExitOverride
                );
                break;
        }
    }

    /// <summary>
    /// 轮流切换：Box -> Conveyor -> Replicator -> Auto -> Box
    /// 方向会继承（Box无方向则用默认 Vector2Int.right）。
    /// </summary>
    public static void CycleAt(
        GridManager2D grid,
        int x, int y,
        AutoMover autoPrefab = null,
        BoxMover boxPrefab = null,
        ConveyorBelt conveyorPrefab = null,
        ReplicatorNode replicatorPrefab = null
    )
    {
        var oldBox = FindAt<BoxMover>(x, y);
        var oldAuto = FindAt<AutoMover>(x, y);
        var oldConv = FindAt<ConveyorBelt>(x, y);
        var oldRep = FindAt<ReplicatorNode>(x, y);

        if (oldBox == null && oldAuto == null && oldConv == null && oldRep == null)
            return;

        var cur = GetCurrentType(oldBox, oldAuto, oldConv, oldRep);
        var next = NextType(cur);

        // 继承方向
        Vector2Int dir = GetDirFromOld(oldAuto, oldConv, oldRep, Vector2Int.right);

        MorphAt(grid, x, y, next,
            dirOverride: dir,
            // Cycle 不知道 entry/exit，所以不传 override -> 用旧规则
            repEntryOverride: null,
            repExitOverride: null,
            autoPrefab: autoPrefab,
            boxPrefab: boxPrefab,
            conveyorPrefab: conveyorPrefab,
            replicatorPrefab: replicatorPrefab);
    }

    // ----------------- helpers -----------------

    static T FindAt<T>(int x, int y) where T : MonoBehaviour
    {
        foreach (var t in Object.FindObjectsOfType<T>())
        {
            if (!t || !t.gameObject.activeSelf) continue;

            // 这些类都有 x/y 字段
            if (t is BoxMover b && b.x == x && b.y == y) return t;
            if (t is AutoMover a && a.x == x && a.y == y) return t;
            if (t is ConveyorBelt c && c.x == x && c.y == y) return t;
            if (t is ReplicatorNode r && r.x == x && r.y == y) return t;
        }
        return null;
    }

    static MaskType GetCurrentType(BoxMover b, AutoMover a, ConveyorBelt c, ReplicatorNode r)
    {
        if (a != null) return MaskType.Auto;
        if (c != null) return MaskType.Conveyor;
        if (r != null) return MaskType.Replicator;
        return MaskType.Box;
    }

    static MaskType NextType(MaskType t)
    {
        return t switch
        {
            MaskType.Box => MaskType.Conveyor,
            MaskType.Conveyor => MaskType.Replicator,
            MaskType.Replicator => MaskType.Auto,
            MaskType.Auto => MaskType.Box,
            _ => MaskType.Box
        };
    }

    static Vector2Int GetDirFromOld(AutoMover a, ConveyorBelt c, ReplicatorNode r, Vector2Int fallback)
    {
        if (a != null) return SafeDir(a.dir);
        if (c != null) return SafeDir(c.dir);
        // 复制器方向我们定义为 exitDir（entryDir 会由规则/override 决定）
        if (r != null) return SafeDir(r.exitDir);
        return SafeDir(fallback);
    }

    static void ApplyDirIfNeeded(
        MaskType t,
        AutoMover a,
        ConveyorBelt c,
        ReplicatorNode r,
        Vector2Int inheritedDir,
        Vector2Int? repEntryOverride,
        Vector2Int? repExitOverride
    )
    {
        inheritedDir = SafeDir(inheritedDir);

        if (t == MaskType.Auto && a != null)
        {
            a.dir = inheritedDir;
            return;
        }

        if (t == MaskType.Conveyor && c != null)
        {
            c.dir = inheritedDir;
            return;
        }

        if (t == MaskType.Replicator && r != null)
        {
            // 如果画线系统给了 entry/exit，则严格采用
            if (repEntryOverride.HasValue && repExitOverride.HasValue)
            {
                Vector2Int entry = SafeDir(repEntryOverride.Value);
                Vector2Int exit = SafeDir(repExitOverride.Value);

                // 必须垂直，否则回退到旧逻辑
                if (entry.x * exit.x + entry.y * exit.y == 0)
                {
                    r.entryDir = entry;
                    r.exitDir = exit;
                    return;
                }
                Debug.LogWarning($"ApplyDirIfNeeded: invalid replicator ports entry={entry} exit={exit}, fallback to legacy.");
            }

            // 旧逻辑：复制器方向定义为 exitDir，entryDir = CW90(exitDir)
            r.exitDir = inheritedDir;
            r.entryDir = new Vector2Int(inheritedDir.y, -inheritedDir.x); // CW 90
        }
    }

    static void SnapExisting(GridManager2D grid, BoxMover b, AutoMover a, ConveyorBelt c, ReplicatorNode r)
    {
        if (b != null) b.SnapToGrid();
        if (a != null) a.SnapToGrid();

        if (c != null)
        {
            Vector3 w = grid.GridToWorld(c.x, c.y);
            c.transform.position = new Vector3(w.x, c.transform.position.y, w.z);
        }

        if (r != null)
        {
            Vector3 w = grid.GridToWorld(r.x, r.y);
            r.transform.position = new Vector3(w.x, r.transform.position.y, w.z);
        }
    }

    static Vector2Int SafeDir(Vector2Int d)
    {
        if (d == Vector2Int.zero) return Vector2Int.right;

        // 只允许四方向
        if (d == Vector2Int.right || d == Vector2Int.left || d == Vector2Int.up || d == Vector2Int.down)
            return d;

        // 兜底：归一到主轴
        if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
            return d.x >= 0 ? Vector2Int.right : Vector2Int.left;
        else
            return d.y >= 0 ? Vector2Int.up : Vector2Int.down;
    }

    static void SpawnAuto(GridManager2D grid, AutoMover prefab, int x, int y, Vector2Int dir)
    {
        dir = SafeDir(dir);

        Vector3 w = grid.GridToWorld(x, y);
        Vector3 pos = new Vector3(w.x, grid.tileTopY + prefab.footYOffset, w.z);

        var go = Object.Instantiate(prefab.gameObject, pos, Quaternion.identity);
        var a = go.GetComponent<AutoMover>();
        a.grid = grid;
        a.x = x; a.y = y;
        a.dir = dir;
        a.SnapToGrid();
    }

    static void SpawnBox(GridManager2D grid, BoxMover prefab, int x, int y)
    {
        Vector3 w = grid.GridToWorld(x, y);
        Vector3 pos = new Vector3(w.x, grid.tileTopY + prefab.yOffset, w.z);

        var go = Object.Instantiate(prefab.gameObject, pos, Quaternion.identity);
        var b = go.GetComponent<BoxMover>();
        b.grid = grid;
        b.x = x; b.y = y;
        b.SnapToGrid();
    }

    static void SpawnConveyor(GridManager2D grid, ConveyorBelt prefab, int x, int y, Vector2Int dir)
    {
        dir = SafeDir(dir);

        Vector3 w = grid.GridToWorld(x, y);
        Vector3 pos = new Vector3(w.x, prefab.transform.position.y, w.z);

        var go = Object.Instantiate(prefab.gameObject, pos, Quaternion.identity);
        var c = go.GetComponent<ConveyorBelt>();
        c.grid = grid;
        c.x = x; c.y = y;
        c.dir = dir;

        // 对齐到格子
        Vector3 ww = grid.GridToWorld(x, y);
        c.transform.position = new Vector3(ww.x, c.transform.position.y, ww.z);
    }

    /// <summary>
    /// 生成复制器：默认旧规则（entry = CW90(exit)）。
    /// 如果 repEntryOverride + repExitOverride 都提供，则严格采用你画线的端口规则：
    /// entry = -seg1, exit = seg2（由外部传入）
    /// </summary>
    static void SpawnReplicator(
        GridManager2D grid,
        ReplicatorNode prefab,
        int x, int y,
        Vector2Int exitDirFallback,
        Vector2Int? repEntryOverride,
        Vector2Int? repExitOverride
    )
    {
        exitDirFallback = SafeDir(exitDirFallback);

        Vector3 w = grid.GridToWorld(x, y);
        Vector3 pos = new Vector3(w.x, prefab.transform.position.y, w.z);

        var go = Object.Instantiate(prefab.gameObject, pos, Quaternion.identity);
        var r = go.GetComponent<ReplicatorNode>();
        r.grid = grid;
        r.x = x; r.y = y;

        // 决定 entry/exit
        Vector2Int exit = exitDirFallback;
        Vector2Int entry = new Vector2Int(exit.y, -exit.x); // legacy default

        if (repEntryOverride.HasValue && repExitOverride.HasValue)
        {
            Vector2Int eIn = SafeDir(repEntryOverride.Value);
            Vector2Int eOut = SafeDir(repExitOverride.Value);

            // 必须垂直，否则回退 legacy
            if (eIn.x * eOut.x + eIn.y * eOut.y == 0)
            {
                entry = eIn;
                exit = eOut;
            }
            else
            {
                Debug.LogWarning($"SpawnReplicator: invalid ports entry={eIn} exit={eOut}, fallback to legacy.");
            }
        }

        r.entryDir = entry;
        r.exitDir = exit;

        // 对齐到格子
        Vector3 ww = grid.GridToWorld(x, y);
        r.transform.position = new Vector3(ww.x, r.transform.position.y, ww.z);
    }

    /// <summary>
    /// 强制在(x,y)生成一个指定类型的mask（不要求原格子上已经有mask）。
    /// 如果该格已有mask，会先销毁再生成。
    /// 用于 Undo/Load/关卡初始化。
    /// </summary>
    public static void SpawnAt(
        GridManager2D grid,
        int x, int y,
        MaskType toType,
        Vector2Int? dirOverride = null,
        Vector2Int? repEntryOverride = null,
        Vector2Int? repExitOverride = null,
        AutoMover autoPrefab = null,
        BoxMover boxPrefab = null,
        ConveyorBelt conveyorPrefab = null,
        ReplicatorNode replicatorPrefab = null
    )
    {
        if (grid == null) { Debug.LogError("SpawnAt: grid is null"); return; }

        // 找旧物体（有就删）
        var oldBox = FindAt<BoxMover>(x, y);
        var oldAuto = FindAt<AutoMover>(x, y);
        var oldConv = FindAt<ConveyorBelt>(x, y);
        var oldRep = FindAt<ReplicatorNode>(x, y);

        if (oldBox) Object.Destroy(oldBox.gameObject);
        if (oldAuto) Object.Destroy(oldAuto.gameObject);
        if (oldConv) Object.Destroy(oldConv.gameObject);
        if (oldRep) Object.Destroy(oldRep.gameObject);

        Vector2Int dir = SafeDir(dirOverride ?? Vector2Int.right);

        // 直接生成（复用你现有的 SpawnXXX）
        switch (toType)
        {
            case MaskType.Box:
                if (boxPrefab == null) { Debug.LogError("SpawnAt: boxPrefab is null"); return; }
                SpawnBox(grid, boxPrefab, x, y);
                break;

            case MaskType.Auto:
                if (autoPrefab == null) { Debug.LogError("SpawnAt: autoPrefab is null"); return; }
                SpawnAuto(grid, autoPrefab, x, y, dir);
                break;

            case MaskType.Conveyor:
                if (conveyorPrefab == null) { Debug.LogError("SpawnAt: conveyorPrefab is null"); return; }
                SpawnConveyor(grid, conveyorPrefab, x, y, dir);
                break;

            case MaskType.Replicator:
                if (replicatorPrefab == null) { Debug.LogError("SpawnAt: replicatorPrefab is null"); return; }
                SpawnReplicator(
                    grid, replicatorPrefab, x, y,
                    exitDirFallback: dir,
                    repEntryOverride: repEntryOverride,
                    repExitOverride: repExitOverride
                );
                break;
        }
    }
}