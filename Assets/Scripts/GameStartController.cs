using System.Collections;
using UnityEngine;

public class GameStartController : MonoBehaviour
{
    public static GameStartController I { get; private set; }

    [Header("UI")]
    public GameObject maskUIPanel;              // Start before gameplay, hide after start.
    public DraggableMaskItem[] draggableItems;  // Mask items in UI.

    [Header("Gameplay")]
    public PlayerMover player;                  // Assign in inspector or auto-find.
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

        // Initial state: lock player, show UI, allow drag.
        SetPlayerEnabled(false);
        SetMaskUI(true);
        SetDraggable(true);

        started = false;

        // Show Auto lethal preview before the first Space input.
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

        // Space after start can be used to force-hide mask UI.
        SetMaskUI(false);
        SetDraggable(false);
    }

    public void StartGame()
    {
        started = true;

        // Hide UI and disable drag.
        SetMaskUI(false);
        SetDraggable(false);

        // Use current equipped mask setup as Undo baseline.
        var undo = FindObjectOfType<StepUndoSystem>();
        if (undo != null)
            StartCoroutine(ResetUndoBaselineNextFrame(undo));

        // Enable player control.
        SetPlayerEnabled(true);

        // Refresh lethal once at game start.
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
        // Wait one frame so AutoMover.Start() has finished.
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
    }
}
