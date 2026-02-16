using UnityEditor;
using UnityEngine;

public class LevelPainterWindow : EditorWindow
{
    // persisted keys
    const string KEY_GRID = "LP_GRID";
    const string KEY_ROOT = "LP_ROOT";
    const string KEY_FLOOR = "LP_FLOOR";
    const string KEY_WALL = "LP_WALL";
    const string KEY_WATER = "LP_WATER";
    const string KEY_GOAL = "LP_GOAL";
    const string KEY_MASKOFF = "LP_MASKOFF";
    const string KEY_LETHAL = "LP_LETHAL";
    const string KEY_CELLSIZE = "LP_CELLSIZE";
    const string KEY_BRUSH = "LP_BRUSH";

    public GridManager2D grid;
    public Transform levelRoot;

    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject waterPrefab;
    public GameObject goalPrefab;
    public GameObject maskDisabledPrefab;
    public GameObject lethalPrefab;

    public float cellSize = 1f;
    public int brush = 0;

    [MenuItem("Tools/Level Painter")]
    public static void Open() => GetWindow<LevelPainterWindow>("Level Painter");

    private void OnEnable()
    {
        LoadPrefs();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SavePrefs();
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();

        grid = (GridManager2D)EditorGUILayout.ObjectField("Grid", grid, typeof(GridManager2D), true);
        levelRoot = (Transform)EditorGUILayout.ObjectField("Level Root", levelRoot, typeof(Transform), true);

        cellSize = EditorGUILayout.FloatField("Cell Size", cellSize);

        GUILayout.Space(8);
        floorPrefab = (GameObject)EditorGUILayout.ObjectField("Floor", floorPrefab, typeof(GameObject), false);
        wallPrefab = (GameObject)EditorGUILayout.ObjectField("Wall", wallPrefab, typeof(GameObject), false);
        waterPrefab = (GameObject)EditorGUILayout.ObjectField("Water", waterPrefab, typeof(GameObject), false);
        goalPrefab = (GameObject)EditorGUILayout.ObjectField("Goal", goalPrefab, typeof(GameObject), false);
        maskDisabledPrefab = (GameObject)EditorGUILayout.ObjectField("MaskDisabled", maskDisabledPrefab, typeof(GameObject), false);
        lethalPrefab = (GameObject)EditorGUILayout.ObjectField("Lethal", lethalPrefab, typeof(GameObject), false);

        GUILayout.Space(8);
        brush = GUILayout.Toolbar(brush, new[] { "Floor", "Wall", "Water", "Goal", "MaskOff", "Lethal" });

        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Scene里：按住 Ctrl + 左键刷格子，Ctrl + 右键删除。\n" +
            "如果点不到：确认有地面 Collider（看下面第2部分）。",
            MessageType.Info);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Find Grid/Root"))
        {
            if (grid == null) grid = FindObjectOfType<GridManager2D>();
            if (levelRoot == null)
            {
                var go = GameObject.Find("_LevelRoot");
                if (go != null) levelRoot = go.transform;
            }
        }
        if (GUILayout.Button("Rebuild Grid From Scene") && grid != null)
            grid.RebuildFromScene();
        GUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
            SavePrefs();
    }

    private void OnSceneGUI(SceneView sv)
    {
        if (grid == null || levelRoot == null) return;

        Event e = Event.current;
        if (!e.control) return;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!RayToGroundPlane(ray, 0f, out Vector3 p)) return; // y=0 平面

        int gx = Mathf.RoundToInt(p.x / cellSize);
        int gy = Mathf.RoundToInt(p.z / cellSize);

        Handles.color = Color.yellow;
        Handles.DrawWireCube(new Vector3(gx * cellSize, 0f, gy * cellSize),
            new Vector3(cellSize, 0.01f, cellSize));

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Paint(gx, gy);
            e.Use();
        }
        else if (e.type == EventType.MouseDown && e.button == 1)
        {
            Erase(gx, gy);
            e.Use();
        }
    }

    private static bool RayToGroundPlane(Ray ray, float planeY, out Vector3 hitPoint)
    {
        // 平面方程：y = planeY
        // Ray: origin + t * dir
        // 解：origin.y + t * dir.y = planeY
        hitPoint = default;

        float dy = ray.direction.y;
        if (Mathf.Abs(dy) < 1e-6f) return false; // 平行于地面

        float t = (planeY - ray.origin.y) / dy;
        if (t < 0f) return false; // 交点在射线背后

        hitPoint = ray.origin + ray.direction * t;
        return true;
    }

    private GameObject GetPrefabByBrush()
    {
        return brush switch
        {
            0 => floorPrefab,
            1 => wallPrefab,
            2 => waterPrefab,
            3 => goalPrefab,
            4 => maskDisabledPrefab,
            5 => lethalPrefab,
            _ => floorPrefab
        };
    }

    private void Paint(int x, int y)
    {
        var prefab = GetPrefabByBrush();
        if (prefab == null) return;

        Erase(x, y);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(levelRoot);
        go.transform.position = new Vector3(x * cellSize, 0f, y * cellSize);
        Undo.RegisterCreatedObjectUndo(go, "Paint Tile");
    }

    private void Erase(int x, int y)
    {
        for (int i = levelRoot.childCount - 1; i >= 0; i--)
        {
            var child = levelRoot.GetChild(i);
            int cx = Mathf.RoundToInt(child.position.x / cellSize);
            int cy = Mathf.RoundToInt(child.position.z / cellSize);
            if (cx == x && cy == y)
                Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    // ---------- Persist helpers ----------
    private void SavePrefs()
    {
        EditorPrefs.SetString(KEY_GRID, ToGuidPath(grid));
        EditorPrefs.SetString(KEY_ROOT, ToGuidPath(levelRoot));
        EditorPrefs.SetString(KEY_FLOOR, ToGuidPath(floorPrefab));
        EditorPrefs.SetString(KEY_WALL, ToGuidPath(wallPrefab));
        EditorPrefs.SetString(KEY_WATER, ToGuidPath(waterPrefab));
        EditorPrefs.SetString(KEY_GOAL, ToGuidPath(goalPrefab));
        EditorPrefs.SetString(KEY_MASKOFF, ToGuidPath(maskDisabledPrefab));
        EditorPrefs.SetString(KEY_LETHAL, ToGuidPath(lethalPrefab));
        EditorPrefs.SetFloat(KEY_CELLSIZE, cellSize);
        EditorPrefs.SetInt(KEY_BRUSH, brush);
    }

    private void LoadPrefs()
    {
        grid = FromGuidPath<GridManager2D>(EditorPrefs.GetString(KEY_GRID, ""));
        levelRoot = FromGuidPath<Transform>(EditorPrefs.GetString(KEY_ROOT, ""));

        floorPrefab = FromGuidPath<GameObject>(EditorPrefs.GetString(KEY_FLOOR, ""));
        wallPrefab = FromGuidPath<GameObject>(EditorPrefs.GetString(KEY_WALL, ""));
        waterPrefab = FromGuidPath<GameObject>(EditorPrefs.GetString(KEY_WATER, ""));
        goalPrefab = FromGuidPath<GameObject>(EditorPrefs.GetString(KEY_GOAL, ""));
        maskDisabledPrefab = FromGuidPath<GameObject>(EditorPrefs.GetString(KEY_MASKOFF, ""));
        lethalPrefab = FromGuidPath<GameObject>(EditorPrefs.GetString(KEY_LETHAL, ""));

        cellSize = EditorPrefs.GetFloat(KEY_CELLSIZE, 1f);
        brush = EditorPrefs.GetInt(KEY_BRUSH, 0);
    }

    private static string ToGuidPath(Object obj)
    {
        if (obj == null) return "";
        string path = AssetDatabase.GetAssetPath(obj);
        if (string.IsNullOrEmpty(path)) return ""; // scene object: can't persist by GUID
        return AssetDatabase.AssetPathToGUID(path);
    }

    private static T FromGuidPath<T>(string guid) where T : Object
    {
        if (string.IsNullOrEmpty(guid)) return null;
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
