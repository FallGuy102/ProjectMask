using UnityEngine;

public class TeleportAnimator : MonoBehaviour
{
    [Tooltip("传送动画时长（秒），建议 0.06~0.12")]
    public float duration = 0.08f;

    private Coroutine co;
    private Vector3 forcedFinalPos;
    private bool hasForcedFinal = false;

    /// <summary>
    /// 开始一次快速传送动画。若已有动画在播，会先把上一段直接结算到终点。
    /// </summary>
    public void PlayTo(Vector3 targetPos, float customDuration = -1f)
    {
        // 如果正在播上一段：直接落到上一段终点
        ForceFinish();

        float d = customDuration > 0 ? customDuration : duration;

        forcedFinalPos = targetPos;
        hasForcedFinal = true;

        co = StartCoroutine(Anim(targetPos, d));
    }

    /// <summary>
    /// 强制结束：立刻到终点（用于 step 很快、或下一次传送到来）
    /// </summary>
    public void ForceFinish()
    {
        if (co != null)
        {
            StopCoroutine(co);
            co = null;
        }

        if (hasForcedFinal)
        {
            transform.position = forcedFinalPos;
            hasForcedFinal = false;
        }
    }

    private System.Collections.IEnumerator Anim(Vector3 targetPos, float d)
    {
        Vector3 from = transform.position;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.001f, d);
            transform.position = Vector3.Lerp(from, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;

        co = null;
        hasForcedFinal = false;
    }
}
