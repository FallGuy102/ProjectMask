using System;
using System.Collections;
using UnityEngine;

public class StepManager : MonoBehaviour
{
    public static StepManager I { get; private set; }

    public int stepIndex { get; private set; } = 0;
    public bool stepping { get; private set; } = false;

    [Header("Step Timing")]
    public float minStepDuration = 0.12f;

    public event Action<int> OnStepBegin;
    public event Action<int> OnStepAfterMove;
    public event Action<int> OnStepResolve;
    public event Action<int> OnStepEnd;

    private PlayerMover _player;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    public void RequestStep(Func<IEnumerator> stepRoutineFactory)
    {
        if (stepping) return;
        StartCoroutine(RunStep(stepRoutineFactory));
    }

    private IEnumerator RunStep(Func<IEnumerator> stepRoutineFactory)
    {
        float stepStartTime = Time.time;

        stepping = true;
        stepIndex++;

        OnStepBegin?.Invoke(stepIndex);

        if (stepRoutineFactory != null)
            yield return stepRoutineFactory.Invoke();

        if (StepMoveSystem.I != null)
            yield return StepMoveSystem.I.ExecuteQueuedMoves();

        OnStepAfterMove?.Invoke(stepIndex);
        OnStepResolve?.Invoke(stepIndex);
        OnStepEnd?.Invoke(stepIndex);

        // Keep step cadence aligned with player animation completion.
        float targetMinDuration = GetTargetMinStepDuration();
        float elapsed = Time.time - stepStartTime;
        float wait = Mathf.Max(0f, targetMinDuration - elapsed);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        stepping = false;
    }

    private float GetTargetMinStepDuration()
    {
        if (_player == null)
            _player = FindObjectOfType<PlayerMover>(includeInactive: true);

        float animDuration = (_player != null) ? _player.animationStepDuration : 0f;
        return Mathf.Max(minStepDuration, animDuration);
    }
}
