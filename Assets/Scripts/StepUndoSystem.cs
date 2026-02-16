using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StepUndoSystem : MonoBehaviour
{
    [Header("Refs")]
    public GridManager2D grid;

    [Header("Prefabs (used to rebuild masks)")]
    public AutoMover autoPrefab;
    public BoxMover boxPrefab;
    public ConveyorBelt conveyorPrefab;
    public ReplicatorNode replicatorPrefab;

    [Header("Input")]
    public KeyCode undoKey = KeyCode.Z;
    public bool requireCtrl = false;

    [Header("History")]
    public int maxHistory = 200;

    [Serializable]
    public struct PlayerState
    {
        public bool exists;
        public bool active;
        public int x;
        public int y;
        public float footYOffset;
        public Vector2Int facingDir;
    }

    [Serializable]
    public struct MaskState
    {
        public MaskType type;
        public int x;
        public int y;
        public Vector2Int dir;
        public float boxYOffset;

        public Vector2Int entryDir;
        public Vector2Int exitDir;

        public AutoState autoState;
    }

    [Serializable]
    public class Snapshot
    {
        public int stepIndex;
        public PlayerState player;
        public List<MaskState> masks = new();
    }

    private readonly List<Snapshot> history = new();
    private bool suppressCapture = false;

    private void OnEnable()
    {
        // On scene reload/switch, ensure history does not leak across scenes.
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (grid == null) grid = FindObjectOfType<GridManager2D>();

        // Initial snapshot: wait for startup initialization before capture.
        StartCoroutine(CaptureInitialAfterInit());

        if (StepManager.I != null)
            StepManager.I.OnStepResolve += OnStepResolve;
    }

    private void OnDestroy()
    {
        if (StepManager.I != null)
            StepManager.I.OnStepResolve -= OnStepResolve;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // After new scene load: clear history and recapture initial state.
        ResetHistoryAndCaptureInitial();
    }

    private IEnumerator CaptureInitialAfterInit()
    {
        yield return null;
        yield return null; // Wait one more frame to avoid capturing before PlayerMover init is synced.
        ResetHistoryAndCaptureInitial();
    }

    /// <summary>
    /// Public helper: clear history and keep only the current level initial snapshot.
    /// Prevents undo falling back to stale snapshots or default player coordinates after reload.
    /// </summary>
    public void ResetHistoryAndCaptureInitial()
    {
        if (grid == null) grid = FindObjectOfType<GridManager2D>();

        history.Clear();

        int step = GetStepIndexSafe();
        PushSnapshot(step);
    }

    private void Update()
    {
        bool pressed = Input.GetKeyDown(undoKey);
        if (!pressed) return;

        if (requireCtrl)
        {
            if (!(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                return;
        }

        UndoOneStep();
    }

    private void OnStepResolve(int step)
    {
        if (suppressCapture) return;
        PushSnapshot(step);
    }

    private void PushSnapshot(int step)
    {
        var s = Capture(step);
        history.Add(s);

        if (history.Count > maxHistory)
            history.RemoveAt(0);
    }

    public void UndoOneStep()
    {
        if (StepManager.I != null && StepManager.I.stepping) return;
        if (history.Count <= 1) return;

        history.RemoveAt(history.Count - 1);
        var prev = history[history.Count - 1];

        suppressCapture = true;
        Restore(prev);
        suppressCapture = false;
    }

    // ===================== Capture =====================

    private Snapshot Capture(int step)
    {
        var s = new Snapshot();
        s.stepIndex = step;

        // Player (includeInactive: true)
        var p = FindObjectOfType<PlayerMover>(includeInactive: true);
        if (p != null)
        {
            s.player.exists = true;
            s.player.active = p.gameObject.activeSelf;
            s.player.footYOffset = p.footYOffset;
            s.player.facingDir = ReadPlayerFacingDir(p);

            // Critical fix: do not trust p.x/p.y, they may still be default values.
            // Derive grid coordinates from transform position to ensure initial snapshot correctness.
            Vector2Int gpos = WorldToGridSafe(p.transform.position, grid);
            s.player.x = gpos.x;
            s.player.y = gpos.y;
        }
        else
        {
            s.player.exists = false;
        }

        // Auto
        foreach (var a in FindObjectsOfType<AutoMover>())
        {
            if (!a || !a.gameObject.activeSelf) continue;
            s.masks.Add(new MaskState
            {
                type = MaskType.Auto,
                x = a.x,
                y = a.y,
                dir = a.dir,
                autoState = a.state
            });
        }

        // Box
        foreach (var b in FindObjectsOfType<BoxMover>())
        {
            if (!b || !b.gameObject.activeSelf) continue;
            s.masks.Add(new MaskState
            {
                type = MaskType.Box,
                x = b.x,
                y = b.y,
                dir = Vector2Int.right,
                boxYOffset = b.yOffset
            });
        }

        // Conveyor
        foreach (var c in FindObjectsOfType<ConveyorBelt>())
        {
            if (!c || !c.gameObject.activeSelf) continue;
            s.masks.Add(new MaskState
            {
                type = MaskType.Conveyor,
                x = c.x,
                y = c.y,
                dir = c.dir
            });
        }

        // Replicator
        foreach (var r in FindObjectsOfType<ReplicatorNode>())
        {
            if (!r || !r.gameObject.activeSelf) continue;
            s.masks.Add(new MaskState
            {
                type = MaskType.Replicator,
                x = r.x,
                y = r.y,
                dir = r.exitDir,
                entryDir = r.entryDir,
                exitDir = r.exitDir
            });
        }

        return s;
    }

    private static Vector2Int WorldToGridSafe(Vector3 worldPos, GridManager2D grid)
    {
        if (grid == null) return Vector2Int.zero;

        // Project uses XZ plane: x -> world.x, y -> world.z.
        // Use grid.cellSize for conversion, consistent with AutoMover.Start.
        int gx = Mathf.RoundToInt(worldPos.x / grid.cellSize);
        int gy = Mathf.RoundToInt(worldPos.z / grid.cellSize);
        return new Vector2Int(gx, gy);
    }

    private static Vector2Int ReadPlayerFacingDir(PlayerMover p)
    {
        var t = p.GetType();

        var f = t.GetField("dir", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(Vector2Int))
            return (Vector2Int)f.GetValue(p);

        f = t.GetField("facingDir", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(Vector2Int))
            return (Vector2Int)f.GetValue(p);

        f = t.GetField("lastDir", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(Vector2Int))
            return (Vector2Int)f.GetValue(p);

        return Vector2Int.up;
    }

    // ===================== Restore =====================

    private void Restore(Snapshot s)
    {
        if (grid == null) grid = FindObjectOfType<GridManager2D>();

        var player = FindObjectOfType<PlayerMover>(includeInactive: true);
        if (player != null) player.StopAllCoroutines();
        if (player != null) player.ClearInputBuffer();

        if (player != null)
            player.gameObject.SetActive(true); // Undo always revives player object.

        // clear masks
        foreach (var a in FindObjectsOfType<AutoMover>()) if (a) { a.gameObject.SetActive(false); Destroy(a.gameObject); }
        foreach (var b in FindObjectsOfType<BoxMover>()) if (b) { b.gameObject.SetActive(false); Destroy(b.gameObject); }
        foreach (var c in FindObjectsOfType<ConveyorBelt>()) if (c) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
        foreach (var r in FindObjectsOfType<ReplicatorNode>()) if (r) { r.gameObject.SetActive(false); Destroy(r.gameObject); }

        // restore player
        if (player != null && s.player.exists)
        {
            player.x = s.player.x;
            player.y = s.player.y;
            player.footYOffset = s.player.footYOffset;

            Vector3 w = grid.GridToWorld(player.x, player.y);
            player.transform.position = new Vector3(w.x, grid.tileTopY + player.footYOffset, w.z);

            WritePlayerFacingDir(player, s.player.facingDir);
            ApplyPlayerFacingVisual(player, s.player.facingDir);

            player.gameObject.SetActive(true);
        }

        // rebuild masks
        var autoStateMap = new Dictionary<Vector2Int, AutoState>();
        var autoDirMap = new Dictionary<Vector2Int, Vector2Int>();
        var boxYOffsetMap = new Dictionary<Vector2Int, float>();

        foreach (var m in s.masks)
        {
            if (m.type == MaskType.Auto)
            {
                autoStateMap[new Vector2Int(m.x, m.y)] = m.autoState;
                autoDirMap[new Vector2Int(m.x, m.y)] = m.dir;
            }
            else if (m.type == MaskType.Box)
            {
                boxYOffsetMap[new Vector2Int(m.x, m.y)] = m.boxYOffset;
            }

            Vector2Int? dirOverride = (m.type == MaskType.Box) ? null : m.dir;

            MaskMorph.SpawnAt(
                grid,
                m.x, m.y,
                m.type,
                dirOverride: dirOverride,
                autoPrefab: autoPrefab,
                boxPrefab: boxPrefab,
                conveyorPrefab: conveyorPrefab,
                replicatorPrefab: replicatorPrefab
            );
        }

        foreach (var a in FindObjectsOfType<AutoMover>())
        {
            if (a == null) continue;
            var key = new Vector2Int(a.x, a.y);

            if (autoStateMap.TryGetValue(key, out var st))
                a.state = st;

            if (autoDirMap.TryGetValue(key, out var d))
                a.SetDirImmediate(d);
        }

        foreach (var b in FindObjectsOfType<BoxMover>())
        {
            if (b == null) continue;
            var key = new Vector2Int(b.x, b.y);
            if (boxYOffsetMap.TryGetValue(key, out var yOffset))
            {
                b.yOffset = yOffset;
                b.SnapToGrid();
            }
        }

        RebuildOccupancy();

        SetStepIndexByReflection(s.stepIndex);

        StartCoroutine(RebuildLethalNextFrame());
    }

    private IEnumerator RebuildLethalNextFrame()
    {
        yield return null;
        if (StepMoveSystem.I != null)
            StepMoveSystem.I.ForceRebuildDynamicLethalFull();
    }

    private static void WritePlayerFacingDir(PlayerMover p, Vector2Int dir)
    {
        if (dir == Vector2Int.zero) dir = Vector2Int.up;

        var t = p.GetType();

        var f = t.GetField("dir", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(Vector2Int))
        {
            f.SetValue(p, dir);
            return;
        }

        f = t.GetField("facingDir", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(Vector2Int))
        {
            f.SetValue(p, dir);
            return;
        }

        f = t.GetField("lastDir", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(Vector2Int))
        {
            f.SetValue(p, dir);
            return;
        }
    }

    private static void ApplyPlayerFacingVisual(PlayerMover p, Vector2Int dir)
    {
        if (dir == Vector2Int.zero) dir = Vector2Int.up;

        var t = p.GetType();
        var m = t.GetMethod("ApplyFacing", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (m != null && m.GetParameters().Length == 0)
        {
            m.Invoke(p, null);
            return;
        }

        Vector3 worldForward =
            (dir == Vector2Int.right) ? Vector3.right :
            (dir == Vector2Int.left) ? Vector3.left :
            (dir == Vector2Int.up) ? Vector3.forward :
            (dir == Vector2Int.down) ? Vector3.back :
            Vector3.forward;

        p.transform.rotation = Quaternion.LookRotation(worldForward, Vector3.up);
    }

    private void RebuildOccupancy()
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

    private int GetStepIndexSafe()
    {
        return StepManager.I != null ? StepManager.I.stepIndex : 0;
    }

    private void SetStepIndexByReflection(int value)
    {
        if (StepManager.I == null) return;

        var f = typeof(StepManager).GetField("<stepIndex>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (f != null)
            f.SetValue(StepManager.I, value);
    }
}


