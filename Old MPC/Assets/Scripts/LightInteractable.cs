using UnityEngine;

public abstract class LightInteractable : MonoBehaviour
{
    protected bool active = false;

    private void Update()
    {
        if (GameManager.Instance.Running)
        {
            if (!active && GameManager.Instance.IsLightAt(transform.position))
                Activate();
            if (active && !GameManager.Instance.IsLightAt(transform.position))
                Deactivate();
        }
    }

    protected abstract void Activate();

    protected abstract void Deactivate();
}