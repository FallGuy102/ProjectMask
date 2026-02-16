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
            return; // 开始后禁止改

        if (_auto == null) return;

        switch (type)
        {
            case MaskTypeSimple.Vertical:
                _auto.state = AutoState.VerticalMove;

                // 如果当前 dir 不是上下，给一个默认朝向（保留你的逻辑也可以）
                if (_auto.dir != Vector2Int.up && _auto.dir != Vector2Int.down)
                    _auto.SetDirImmediate(Vector2Int.up);
                break;

            case MaskTypeSimple.Horizontal:
                _auto.state = AutoState.HorizontalMove;

                // 如果当前 dir 不是左右，给一个默认朝向
                if (_auto.dir != Vector2Int.left && _auto.dir != Vector2Int.right)
                    _auto.SetDirImmediate(Vector2Int.right);
                break;
        }

        // 让模型朝向立刻更新（如果你 AutoMover 里 ApplyFacing 是 private，就靠 SetDirImmediate）
        // _auto.ApplyFacing(); // 不需要
    }
}
