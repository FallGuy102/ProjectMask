using System.Collections;
using UnityEngine;

namespace Controller
{
    public abstract class GridMover : MonoBehaviour
    {
        public float moveSpeed = 5f;
        protected bool moving = false;
        [HideInInspector] public bool moved = false;

        private void Awake()
        {
            // Snap to grid on start
            var pos = transform.position;
            transform.position = new Vector3(Mathf.Round(pos.x), Mathf.Round(pos.y), Mathf.Round(pos.z));
        }

        public IEnumerator Move(Vector3 dir)
        {
            // Return if already moved
            if (moved)
                yield break;

            // Check if movement is possible
            if (!GameManager.Instance.CanMoveTo(transform.position + dir))
                yield break;

            // Start moving
            moving = true;
            var start = transform.position;
            var target = start + dir;

            // Smoothly move to the target position
            float movement = 0;
            while (movement < 1f)
            {
                movement += Time.deltaTime * moveSpeed;
                transform.position = Vector3.Lerp(start, target, movement);
                yield return null;
            }

            // Ensure final position is exactly the target
            transform.position = target;
            moving = false;
            AfterMove();
        }

        protected virtual void AfterMove()
        {
            moved = true;
        }
    }
}