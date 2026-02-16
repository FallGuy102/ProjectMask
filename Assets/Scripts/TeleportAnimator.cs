using UnityEngine;

public class TeleportAnimator : MonoBehaviour
{
    [Tooltip("0.06~0.12")]
    public float duration = 0.08f;

    private Coroutine co;
    private Vector3 forcedFinalPos;
    private bool hasForcedFinal = false;

    /// <summary>
    /// Start a short teleport animation. If another one is running, finish the previous segment first.
    /// </summary>
    public void PlayTo(Vector3 targetPos, float customDuration = -1f)
    {
        // If a previous segment is still running, snap to its end first.
        ForceFinish();

        float d = customDuration > 0 ? customDuration : duration;

        forcedFinalPos = targetPos;
        hasForcedFinal = true;

        co = StartCoroutine(Anim(targetPos, d));
    }

    /// <summary>
    /// Force finish: jump to the destination immediately (used for very fast steps or a new teleport).
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
