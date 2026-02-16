using UnityEngine;

namespace Mask
{
    public abstract class AnimalMask : MonoBehaviour
    {
        [Header("Move Settings")] public Vector3Int movement = Vector3Int.forward;

        [Header("Light Settings")] public int lightRangeForward = 1;
        public int lightRangeBack = 1;
        public int lightRangeLeft = 1;
        public int lightRangeRight = 1;

        [Header("Visual Settings")] public Color maskColor;

        // Dragging Settings
        private bool _dragging;
        private Camera _cam;
        private Vector3 _startPos;
        private Transform _startParent;

        private void Awake()
        {
            GetComponent<Renderer>().material.color = maskColor;
            _cam = Camera.main;
        }

        public abstract void BeforeMove();

        private void OnMouseDown()
        {
            if (GameManager.Instance.Running) return; // Return if the game is running
            _dragging = true;
            _startPos = transform.position;
            _startParent = transform.parent;
            transform.SetParent(null);
        }

        private void OnMouseDrag()
        {
            if (!_dragging) return;
            transform.position = GetGridPosition(GetMouseWorldPos());
        }

        private void OnMouseUp()
        {
            if (!_dragging) return;
            _dragging = false;
            var npc = GameManager.Instance.CanPlaceMask(this);
            if (npc)
            {
                // If another mask is already equipped, return it to its start position
                var mask = npc.GetComponentInChildren<AnimalMask>();
                if (mask)
                {
                    mask.transform.position = _startPos;
                    mask.transform.SetParent(_startParent);
                }

                // Equip this mask to the NPC
                transform.position = npc.transform.position;
                transform.SetParent(npc.transform);
            }
            else
            {
                transform.position = _startPos;
                transform.SetParent(_startParent);
            }
        }

        private static Vector3 GetGridPosition(Vector3 position)
        {
            return new Vector3(Mathf.Round(position.x), 0f, Mathf.Round(position.z));
        }

        private Vector3 GetMouseWorldPos()
        {
            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.up, Vector3.zero); // y = 0 plane
            if (ground.Raycast(ray, out var enter))
                return ray.GetPoint(enter);
            return Vector3.zero;
        }
    }
}