using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StepResolver : MonoBehaviour
{
    public GridManager2D grid;
    public PlayerMover player;

    [Header("Level Flow")]
    public float winDelay = 0.2f;

    private bool transitioning;

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
        if (transitioning) return;
        if (grid == null || player == null || !player.gameObject.activeSelf) return;

        if (grid.IsLethal(player.x, player.y))
        {
            Debug.Log($"Step {step}: Player killed!");
            player.gameObject.SetActive(false);
            return;
        }

        if (grid.IsGoal(player.x, player.y))
        {
            Debug.Log($"Step {step}: GOAL reached! Loading next level...");
            StartCoroutine(LoadNextLevel());
        }
    }

    private IEnumerator LoadNextLevel()
    {
        transitioning = true;

        if (player != null)
            player.enabled = false;

        if (winDelay > 0f)
            yield return new WaitForSeconds(winDelay);

        var active = SceneManager.GetActiveScene();
        int count = SceneManager.sceneCountInBuildSettings;
        if (count <= 0) yield break;

        int next = active.buildIndex + 1;
        if (next >= count)
            next = 0;

        SceneManager.LoadScene(next);
    }
}
