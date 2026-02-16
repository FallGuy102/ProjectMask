using UnityEngine;

public class Ground : LightInteractable
{
    private MeshRenderer _meshRenderer;

    private void Awake()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
        Deactivate();
    }

    protected override void Activate()
    {
        _meshRenderer.material.color = Color.yellow;
        active = true;
    }

    protected override void Deactivate()
    {
        _meshRenderer.material.color = Color.gray;
        active = false;
    }
}