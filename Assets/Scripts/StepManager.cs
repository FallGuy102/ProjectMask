using System;
using System.Collections;
using UnityEngine;

public class StepManager : MonoBehaviour
{
    public static StepManager I { get; private set; }

    public int stepIndex { get; private set; } = 0;
    public bool stepping { get; private set; } = false;

    // 事件：以后给复制器/传送带/机关订阅
    public event Action<int> OnStepBegin;
    public event Action<int> OnStepAfterMove;
    public event Action<int> OnStepResolve;
    public event Action<int> OnStepEnd;

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
        stepping = true;
        stepIndex++;

        OnStepBegin?.Invoke(stepIndex);

        // 执行：移动阶段（玩家/自动单位以后都在这一步做）
        // 1) 玩家本次输入导致的移动
        if (stepRoutineFactory != null)
            yield return stepRoutineFactory.Invoke();

        // 2) 自动单位移动（MOVE机器人等）
        if (StepMoveSystem.I != null)
            yield return StepMoveSystem.I.ExecuteQueuedMoves();

        OnStepAfterMove?.Invoke(stepIndex);

        // 执行：结算阶段（复制器、压力板、死亡区等以后都放这里）
        OnStepResolve?.Invoke(stepIndex);

        OnStepEnd?.Invoke(stepIndex);

        stepping = false;
    }
}
