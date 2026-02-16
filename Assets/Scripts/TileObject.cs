using UnityEngine;

public enum TileType
{
    Floor,
    Wall,
    Water,
    Goal,
    MaskDisabled,
    Lethal
}

public class TileObject : MonoBehaviour
{
    [Header("Logic")]
    public TileType tileType = TileType.Floor;

    public bool maskEditDisabled = false;

    // Dynamic lethal (set by autos each step)
    public bool lethal = false;

    [Header("Grid Coord")]
    public int x;
    public int y;

    [Header("Visual (optional)")]
    [Tooltip("Renderer of the tile mesh. If null, will search in children.")]
    public Renderer rend;

    [Tooltip("Dynamic lethal color (bright yellow). Override in Inspector if you like.")]
    public Color lethalColor = new Color(1f, 1f, 0.2f, 1f);

    private bool _visualInited;
    private Color _baseColor;
    private MaterialPropertyBlock _mpb;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        InitVisual();
        ApplyColor(lethal ? lethalColor : _baseColor);
    }

    private void InitVisual()
    {
        if (_visualInited) return;
        _visualInited = true;

        if (rend == null)
            rend = GetComponentInChildren<Renderer>();

        _mpb = new MaterialPropertyBlock();

        if (rend != null && rend.sharedMaterial != null)
        {
            var m = rend.sharedMaterial;
            if (m.HasProperty(BaseColorId)) _baseColor = m.GetColor(BaseColorId);
            else if (m.HasProperty(ColorId)) _baseColor = m.GetColor(ColorId);
            else _baseColor = Color.white;
        }
        else
        {
            _baseColor = Color.white;
        }
    }

    /// <summary>
    /// Set/clear dynamic lethal. Also updates tile color (via MaterialPropertyBlock).
    /// </summary>
    public void SetDynamicLethal(bool on)
    {
        InitVisual();
        lethal = on;
        ApplyColor(on ? lethalColor : _baseColor);
    }

    private void ApplyColor(Color c)
    {
        if (rend == null) return;

        rend.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColorId, c);
        _mpb.SetColor(ColorId, c);
        rend.SetPropertyBlock(_mpb);
    }
}
