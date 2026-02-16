using UnityEngine;

public class StepResolver : MonoBehaviour
{
    public GridManager2D grid;
    public PlayerMover player;

    private void Start()
    {
        if (grid == null) grid = FindObjectOfType<GridManager2D>();
        if (player == null) player = FindObjectOfType<PlayerMover>();

        if (StepManager.I != null)
            StepManager.I.OnStepResolve += HandleResolve;
    }

    private void OnDestroy()
    {
        if (StepManager.I != null)
            StepManager.I.OnStepResolve -= HandleResolve;
    }

    private void HandleResolve(int step)
    {
        if (grid == null || player == null || !player.gameObject.activeSelf) return;

        if (grid.IsLethal(player.x, player.y))
        {
            Debug.Log($"Step {step}: Player killed!");
            player.gameObject.SetActive(false);
            return;
        }

        if (grid.IsGoal(player.x, player.y))
        {
            Debug.Log($"Step {step}: GOAL reached!");
        }
    }
}
