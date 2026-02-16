using System.Collections;
using System.Collections.Generic;
using Controller;
using Mask;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private Flag _flag;
    private Player _player;
    public List<NPC> npcs = new List<NPC>();
    private readonly List<Ground> _grounds = new List<Ground>();
    private bool _npcsActing = false;

    public bool Running { get; set; }

    private void Awake()
    {
        if (Instance)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        _flag = FindObjectOfType<Flag>();
        _player = FindObjectOfType<Player>();
        _grounds.AddRange(FindObjectsOfType<Ground>());
    }

    private void Update()
    {
        // Restart the scene
        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // Start the game on space key press
        if (!Running && Input.GetKeyDown(KeyCode.Space))
        {
            Running = true;
            Debug.Log("Game Started!");
        }

        // Main game loop
        if (Running)
        {
            // Always update the player first
            _player.OnUpdate();

            // Only update NPCs if the player has moved
            if (_player.moved && !_npcsActing)
            {
                // Check if the player is in light
                if (IsLightAt(_player.transform.position))
                {
                    Debug.Log("Player caught in light! Game Over.");
                    Running = false;
                    return;
                }

                // Check if the player has reached the flag
                if (IsSameGrid(_player.transform.position, _flag.transform.position))
                {
                    Debug.Log("Player reached the flag! You win!");
                    Running = false;
                    return;
                }

                // Update all NPCs
                StartCoroutine(NPCTurn());
            }
        }
    }

    private IEnumerator NPCTurn()
    {
        // Set flag to prevent player input during NPC turns
        _npcsActing = true;
        foreach (var npc in npcs)
        {
            var mask = npc.GetEquippedMask();
            if (mask)
            {
                mask.BeforeMove();
                yield return npc.Move(mask.movement);
            }
        }

        // Reset moved flags after all NPCs have acted
        foreach (var npc in npcs)
            npc.moved = false;
        _npcsActing = false;
        _player.moved = false;
    }

    public bool CanMoveTo(Vector3 pos)
    {
        // Check if any NPC occupies the target position
        foreach (var npc in npcs)
            if (IsSameGrid(npc.transform.position, pos))
                return false;

        // Check if the target position is a valid grid position
        foreach (var grid in _grounds)
            if (IsSameGrid(grid.transform.position, pos))
                return true;

        // Position is not valid
        return false;
    }

    public bool IsLightAt(Vector3 pos)
    {
        foreach (var npc in npcs)
        foreach (var tile in npc.GetLightTiles())
            if (IsSameGrid(tile, pos))
                return true;
        return false;
    }

    public NPC CanPlaceMask(AnimalMask mask)
    {
        foreach (var npc in npcs)
            if (IsSameGrid(npc.transform.position, mask.transform.position))
                return npc;
        return null;
    }

    private static bool IsSameGrid(Vector3 a, Vector3 b)
    {
        a.y = 0;
        b.y = 0;
        return Vector3.Distance(a, b) < 0.1f;
    }
}