using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelResetter : MonoBehaviour
{
    [Header("Input")]
    public KeyCode resetKey = KeyCode.R;

    [Header("Options")]
    public bool requireCtrl = false;

    private void Update()
    {
        if (!Input.GetKeyDown(resetKey)) return;

        if (requireCtrl &&
            !(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            return;

        ResetLevel();
    }

    public void ResetLevel()
    {
        // 防止 Time.timeScale 被暂停导致重载异常
        Time.timeScale = 1f;

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
