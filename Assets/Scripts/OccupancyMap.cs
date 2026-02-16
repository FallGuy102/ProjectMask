using System.Collections.Generic;
using UnityEngine;

public class OccupancyMap : MonoBehaviour
{
    public static OccupancyMap I { get; private set; }
    private readonly Dictionary<Vector2Int, object> occ = new();

    private void Awake() => I = this;

    public void Clear() => occ.Clear();

    public void Set(int x, int y, object who) => occ[new Vector2Int(x, y)] = who;

    public object Get(int x, int y)
    {
        occ.TryGetValue(new Vector2Int(x, y), out var v);
        return v;
    }

    public bool IsOccupied(int x, int y) => Get(x, y) != null;

    // ✅ 新增：每步开始/每次玩家尝试移动前都可以调用
    public void Rebuild(PlayerMover player, IEnumerable<AutoMover> autos)
    {
        Clear();

        if (player != null && player.gameObject.activeSelf)
            Set(player.x, player.y, player);

        if (autos != null)
        {
            foreach (var a in autos)
            {
                if (a == null || !a.gameObject.activeSelf) continue;
                Set(a.x, a.y, a);
            }
        }
    }
}
