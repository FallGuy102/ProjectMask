using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class DraggableMaskItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public MaskTypeSimple maskType;

    [Header("Drag Settings")]
    public Canvas rootCanvas;           // 拖 UI 时要用的 Canvas（建议拖进来）
    public Camera worldCamera;          // 用于射线投射到 Auto（默认 MainCamera）
    public float worldRayMaxDistance = 200f;

    [Header("Visual")]
    public bool returnToOriginOnDrop = true;

    private CanvasGroup _cg;
    private RectTransform _rt;
    private Vector3 _originPos;
    private bool _interactable = true;

    private void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
        _rt = GetComponent<RectTransform>();
        _originPos = _rt.position;

        if (worldCamera == null) worldCamera = Camera.main;
    }

    public void SetInteractable(bool on)
    {
        _interactable = on;
        _cg.alpha = on ? 1f : 0.5f;
        _cg.blocksRaycasts = on;
        _cg.interactable = on;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_interactable) return;
        if (GameStartController.I != null && GameStartController.I.started) return;

        _originPos = _rt.position;

        // 拖拽过程中让 UI raycast 穿透（否则 EndDrag 时可能挡住射线）
        _cg.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_interactable) return;
        if (GameStartController.I != null && GameStartController.I.started) return;

        if (rootCanvas == null)
        {
            // fallback：直接用屏幕坐标
            _rt.position = eventData.position;
            return;
        }

        // 让 UI 跟随鼠标（考虑 Canvas scale）
        _rt.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_interactable) return;

        // 恢复 raycast
        _cg.blocksRaycasts = true;

        if (GameStartController.I != null && GameStartController.I.started)
        {
            if (returnToOriginOnDrop) _rt.position = _originPos;
            return;
        }

        // 用鼠标位置向世界投射，找 AutoMaskReceiver
        if (worldCamera == null) worldCamera = Camera.main;

        bool equipped = false;

        Ray ray = worldCamera.ScreenPointToRay(eventData.position);
        if (Physics.Raycast(ray, out RaycastHit hit, worldRayMaxDistance))
        {
            var receiver = hit.collider.GetComponentInParent<AutoMaskReceiver>();
            if (receiver != null)
            {
                receiver.Equip(maskType);
                equipped = true;
            }
        }

        if (returnToOriginOnDrop)
            _rt.position = _originPos;

        // 可选：装备后立刻刷新 lethal/颜色
        if (equipped && StepMoveSystem.I != null)
            StepMoveSystem.I.ForceRebuildDynamicLethalFull();
    }
}
