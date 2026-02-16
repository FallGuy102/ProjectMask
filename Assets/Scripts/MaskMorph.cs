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
    /// Switch the mask object on a tile to the target type.
    /// Supports conversion among Box / Auto / Conveyor / Replicator.
    /// dirOverride applies to Auto/Conveyor direction (ignored for Box).
    /// For Replicator, prefer repEntryOverride + repExitOverride.
    /// Without dirOverride, inherit previous direction if possible; Box falls back to Vector2Int.right.
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

        // Find existing mask objects on this tile.
        var oldBox = FindAt<BoxMover>(x, y);
        var oldAuto = FindAt<AutoMover>(x, y);
        var oldConv = FindAt<ConveyorBelt>(x, y);
        var oldRep = FindAt<ReplicatorNode>(x, y);

        // No mask on tile, nothing to process.
        if (oldBox == null && oldAuto == null && oldConv == null && oldRep == null)
            return;

        // Direction inheritance priority: dirOverride first, otherwise old object state.
        Vector2Int inheritedDir = dirOverride ?? GetDirFromOld(oldAuto, oldConv, oldRep, Vector2Int.right);
        inheritedDir = SafeDir(inheritedDir);

        // If target type equals current type, only update direction and snap alignment.
        var curType = GetCurrentType(oldBox, oldAuto, oldConv, oldRep);
        if (curType == toType)
        {
            ApplyDirIfNeeded(toType, oldAuto, oldConv, oldRep, inheritedDir, repEntryOverride, repExitOverride);
            SnapExisting(grid, oldBox, oldAuto, oldConv, oldRep);
            return;
        }

        // Destroy old mask object before conversion.
        if (oldBox) Object.Destroy(oldBox.gameObject);
        if (oldAuto) Object.Destroy(oldAuto.gameObject);
        if (oldConv) Object.Destroy(oldConv.gameObject);
        if (oldRep) Object.Destroy(oldRep.gameObject);

        // Spawn the new target mask object.
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
    /// Cycle types: Box -> Conveyor -> Replicator -> Auto -> Box.
    /// Direction is inherited (Box uses Vector2Int.right fallback).
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

        // Inherit direction from old object.
        Vector2Int dir = GetDirFromOld(oldAuto, oldConv, oldRep, Vector2Int.right);

        MorphAt(grid, x, y, next,
            dirOverride: dir,
            // Cycle mode does not know entry/exit, so it uses legacy replicator rules.
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

            // All mask types here expose x/y fields.
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
        // Replicator direction is defined by exitDir (entryDir comes from rules/override).
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
            // If line-drawing provides entry/exit, apply them strictly.
            if (repEntryOverride.HasValue && repExitOverride.HasValue)
            {
                Vector2Int entry = SafeDir(repEntryOverride.Value);
                Vector2Int exit = SafeDir(repExitOverride.Value);

                // entry and exit must be perpendicular, otherwise fallback to legacy logic.
                if (entry.x * exit.x + entry.y * exit.y == 0)
                {
                    r.entryDir = entry;
                    r.exitDir = exit;
                    return;
                }
                Debug.LogWarning($"ApplyDirIfNeeded: invalid replicator ports entry={entry} exit={exit}, fallback to legacy.");
            }

            // Legacy logic: exitDir defines facing, entryDir = CW90(exitDir).
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

        // Allow only four cardinal directions.
        if (d == Vector2Int.right || d == Vector2Int.left || d == Vector2Int.up || d == Vector2Int.down)
            return d;

        // Fallback: normalize to dominant axis.
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

        // Snap object to grid cell.
        Vector3 ww = grid.GridToWorld(x, y);
        c.transform.position = new Vector3(ww.x, c.transform.position.y, ww.z);
    }

    /// <summary>
    /// Spawn replicator with legacy default: entry = CW90(exit).
    /// If both repEntryOverride and repExitOverride are provided, use drawn-port rules strictly.
    /// entry = -seg1, exit = seg2 (provided by caller).
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

        // Resolve entry/exit directions.
        Vector2Int exit = exitDirFallback;
        Vector2Int entry = new Vector2Int(exit.y, -exit.x); // legacy default

        if (repEntryOverride.HasValue && repExitOverride.HasValue)
        {
            Vector2Int eIn = SafeDir(repEntryOverride.Value);
            Vector2Int eOut = SafeDir(repExitOverride.Value);

            // Must be perpendicular; otherwise fallback to legacy setup.
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

        // Snap object to grid cell.
        Vector3 ww = grid.GridToWorld(x, y);
        r.transform.position = new Vector3(ww.x, r.transform.position.y, ww.z);
    }

    /// <summary>
    /// Force-spawn a mask of the specified type at (x, y), even if no mask existed.
    /// If a mask already exists on this tile, destroy it first.
    /// Used by Undo/Load/level initialization.
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

        // Find old objects on tile and delete them if present.
        var oldBox = FindAt<BoxMover>(x, y);
        var oldAuto = FindAt<AutoMover>(x, y);
        var oldConv = FindAt<ConveyorBelt>(x, y);
        var oldRep = FindAt<ReplicatorNode>(x, y);

        if (oldBox) Object.Destroy(oldBox.gameObject);
        if (oldAuto) Object.Destroy(oldAuto.gameObject);
        if (oldConv) Object.Destroy(oldConv.gameObject);
        if (oldRep) Object.Destroy(oldRep.gameObject);

        Vector2Int dir = SafeDir(dirOverride ?? Vector2Int.right);

        // Spawn directly by reusing existing SpawnXXX helpers.
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
