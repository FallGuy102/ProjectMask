using UnityEngine;

[RequireComponent(typeof(AutoMover))]
public class AutoMaskReceiver : MonoBehaviour
{
    private AutoMover _auto;

    private void Awake()
    {
        _auto = GetComponent<AutoMover>();
    }

    public void Equip(MaskTypeSimple type)
    {
        if (GameStartController.I != null && GameStartController.I.started)
            return; // Do not allow editing after game start.

        if (_auto == null) return;

        switch (type)
        {
            case MaskTypeSimple.Vertical:
                _auto.state = AutoState.VerticalMove;

                // If current dir is not vertical, assign a default vertical direction.
                if (_auto.dir != Vector2Int.up && _auto.dir != Vector2Int.down)
                    _auto.SetDirImmediate(Vector2Int.up);
                break;

            case MaskTypeSimple.Horizontal:
                _auto.state = AutoState.HorizontalMove;

                // If current dir is not horizontal, assign a default horizontal direction.
                if (_auto.dir != Vector2Int.left && _auto.dir != Vector2Int.right)
                    _auto.SetDirImmediate(Vector2Int.right);
                break;
        }

        // SetDirImmediate already refreshes facing; no direct ApplyFacing call is needed.
        // SetDirImmediate already updates facing; no need to call ApplyFacing directly.
    }
}
