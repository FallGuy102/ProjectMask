using UnityEngine;

namespace Controller
{
    public class Player : GridMover
    {
        private void Update()
        {
            // Check if the player is in light
            if (GameManager.Instance.IsLightAt(transform.position))
            {
                Debug.Log("Player caught in light! Game Over.");
                GameManager.Instance.Running = false;
            }
        }

        public void OnUpdate()
        {
            // Do not accept input while moving or if already moved this turn
            if (moving || moved)
                return;

            // Get input direction
            var dir = Vector3.zero;
            if (Input.GetKeyDown(KeyCode.W)) dir = Vector3.forward;
            if (Input.GetKeyDown(KeyCode.S)) dir = Vector3.back;
            if (Input.GetKeyDown(KeyCode.A)) dir = Vector3.left;
            if (Input.GetKeyDown(KeyCode.D)) dir = Vector3.right;

            // Start moving if a direction was pressed
            if (dir != Vector3.zero)
            {
                StartCoroutine(Move(dir));
            }
        }
    }
}