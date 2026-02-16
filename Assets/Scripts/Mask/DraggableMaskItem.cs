using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class DraggableMaskItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public MaskTypeSimple maskType;

    [Header("Drag Settings")]
    public Canvas rootCanvas;           // Canvas used for dragging UI item (recommended to assign in inspector).
    public Camera worldCamera;          // Camera used for world raycast to Auto target (default: MainCamera).
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

        // Allow UI raycast passthrough while dragging (avoid blocking world raycast on drop).
        _cg.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_interactable) return;
        if (GameStartController.I != null && GameStartController.I.started) return;

        if (rootCanvas == null)
        {
            // Fallback: use screen coordinates directly.
            _rt.position = eventData.position;
            return;
        }

        // Keep UI element following cursor (respecting Canvas scale).
        _rt.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_interactable) return;

        // Restore raycast behavior.
        _cg.blocksRaycasts = true;

        if (GameStartController.I != null && GameStartController.I.started)
        {
            if (returnToOriginOnDrop) _rt.position = _originPos;
            return;
        }

        // Raycast from cursor position into world to find AutoMaskReceiver.
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

        // Optional: refresh lethal/visual state immediately after equip.
        if (equipped && StepMoveSystem.I != null)
            StepMoveSystem.I.ForceRebuildDynamicLethalFull();
    }
}
