using System.Collections;
using UnityEngine;

public class GameStartController : MonoBehaviour
{
    public static GameStartController I { get; private set; }

    [Header("UI")]
    public GameObject maskUIPanel;            // 整个 UI 面板（开始后隐藏�?
    public DraggableMaskItem[] draggableItems; // UI 中两个可拖拽 Mask

    [Header("Gameplay")]
    public PlayerMover player;               // 拖进来（或自�?Find�?
    public KeyCode startKey = KeyCode.Space;

    [Header("State (ReadOnly)")]
    public bool started = false;

    private void Awake()
    {
        I = this;
    }

    private void Start()
    {
        if (player == null) player = FindObjectOfType<PlayerMover>(includeInactive: true);

        // 开局：锁玩家、开 UI、允许拖�?
        SetPlayerEnabled(false);
        SetMaskUI(true);
        SetDraggable(true);

        started = false;

        // Build initial dynamic lethal preview before the first start input.
        StartCoroutine(RebuildInitialLethalNextFrame());
    }

    private void Update()
    {
        if (!Input.GetKeyDown(startKey)) return;

        if (!started)
        {
            StartGame();
            return;
        }

        // If UI is shown again during runtime, Space hides it immediately.
        SetMaskUI(false);
        SetDraggable(false);
    }

    public void StartGame()
    {
        started = true;

        // �?UI，禁拖拽
        SetMaskUI(false);
        SetDraggable(false);

        // �?关键：把“当前配置（戴好�?mask）”作�?Undo 的初始基�?
        var undo = FindObjectOfType<StepUndoSystem>();
        if (undo != null)
        {
            // 等一帧，确保所�?Equip/SetStateAndDir/SetDirImmediate 等都落地
            StartCoroutine(ResetUndoBaselineNextFrame(undo));
        }

        // 放开玩家
        SetPlayerEnabled(true);

        // 可选：开始那一刻刷�?lethal
        if (StepMoveSystem.I != null)
            StepMoveSystem.I.ForceRebuildDynamicLethalFull();
    }

    private IEnumerator ResetUndoBaselineNextFrame(StepUndoSystem undo)
    {
        yield return null;
        undo.ResetHistoryAndCaptureInitial();
    }

    private IEnumerator RebuildInitialLethalNextFrame()
    {
        // Wait for AutoMover Start() initialization, then paint lethal preview.
        yield return null;
        if (!started && StepMoveSystem.I != null)
            StepMoveSystem.I.ForceRebuildDynamicLethalFull();
    }


    private void SetMaskUI(bool on)
    {
        if (maskUIPanel != null)
            maskUIPanel.SetActive(on);
    }

    private void SetDraggable(bool on)
    {
        if (draggableItems == null) return;
        foreach (var d in draggableItems)
            if (d != null) d.SetInteractable(on);
    }

    private void SetPlayerEnabled(bool on)
    {
        if (player == null) return;
        player.enabled = on;

        // 如果�?PlayerMover 还有别的输入脚本，也可以在这里一起关
        // e.g. player.GetComponent<PlayerInput>()?.enabled = on;
    }
}

