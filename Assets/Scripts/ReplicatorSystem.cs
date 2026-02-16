using System.Collections.Generic;
using UnityEngine;

public class ReplicatorSystem : MonoBehaviour
{
    public GridManager2D grid;

    [Header("Prefabs (for type conversion)")]
    public AutoMover autoPrefab;
    public BoxMover boxPrefab;

    private void Start()
    {
        if (grid == null) grid = FindObjectOfType<GridManager2D>();
        StepManager.I.OnStepResolve += HandleResolve;
    }

    private void OnDestroy()
    {
        if (StepManager.I != null) StepManager.I.OnStepResolve -= HandleResolve;
    }

    private void HandleResolve(int step)
    {
        var nodes = FindObjectsOfType<ReplicatorNode>();
        if (nodes == null || nodes.Length == 0) return;

        // 快照：本步只关心 Box / Auto
        var boxMap = new Dictionary<Vector2Int, BoxMover>();
        foreach (var b in FindObjectsOfType<BoxMover>())
            if (b != null && b.gameObject.activeSelf)
                boxMap[new Vector2Int(b.x, b.y)] = b;

        var autoMap = new Dictionary<Vector2Int, AutoMover>();
        foreach (var a in FindObjectsOfType<AutoMover>())
            if (a != null && a.gameObject.activeSelf)
                autoMap[new Vector2Int(a.x, a.y)] = a;

        var usedExit = new HashSet<Vector2Int>();

        foreach (var node in nodes)
        {
            if (node == null || !node.gameObject.activeSelf) continue;

            int rotSign = node.Rot90Sign();
            if (rotSign == 0)
            {
                Debug.LogWarning($"ReplicatorNode at ({node.x},{node.y}) entry/exit not perpendicular.");
                continue;
            }

            Vector2Int entry = node.EntryCell;
            Vector2Int exit = node.ExitCell;

            if (usedExit.Contains(exit)) continue;

            bool entryIsBox = boxMap.TryGetValue(entry, out var entryBox);
            bool entryIsAuto = autoMap.TryGetValue(entry, out var entryAuto);
            if (!entryIsBox && !entryIsAuto) continue;

            bool exitIsBox = boxMap.TryGetValue(exit, out var exitBox);
            bool exitIsAuto = autoMap.TryGetValue(exit, out var exitAuto);
            if (!exitIsBox && !exitIsAuto) continue;

            if (autoPrefab == null || boxPrefab == null)
            {
                Debug.LogError("ReplicatorSystem: autoPrefab / boxPrefab not assigned.");
                return;
            }

            // ===== 统一走 MaskMorph（切换 mask 的入口函数）=====
            if (entryIsAuto)
            {
                Vector2Int newDir = Rotate90(entryAuto.dir, rotSign);

                // 出口无论原来是 box 还是 auto，都 Morph 成 Auto，并设置方向
                MaskMorph.MorphAt(
                    grid,
                    exit.x, exit.y,
                    MaskType.Auto,
                    dirOverride: newDir,
                    autoPrefab: autoPrefab,
                    boxPrefab: boxPrefab,
                    conveyorPrefab: null,
                    replicatorPrefab: null
                );
            }
            else // entryIsBox
            {
                // 出口 Morph 成 Box（无方向）
                MaskMorph.MorphAt(
                    grid,
                    exit.x, exit.y,
                    MaskType.Box,
                    dirOverride: null,
                    autoPrefab: autoPrefab,
                    boxPrefab: boxPrefab,
                    conveyorPrefab: null,
                    replicatorPrefab: null
                );
            }

            usedExit.Add(exit);
        }
    }

    private Vector2Int Rotate90(Vector2Int v, int sign)
    {
        // sign=+1 CCW: (x,y)->(-y,x)
        // sign=-1 CW : (x,y)->(y,-x)
        return sign >= 0 ? new Vector2Int(-v.y, v.x) : new Vector2Int(v.y, -v.x);
    }
}