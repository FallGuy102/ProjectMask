using System.Collections.Generic;
using UnityEngine;

public class Grids : MonoBehaviour
{
    private static Grids _instance;

    public float gridSpacing = 0f;
    public Vector3 gridsSize = new Vector3(5, 1, 5);
    public GameObject gridPrefab;
    [HideInInspector] public List<Vector3> gridPositions = new List<Vector3>();

    private void Awake()
    {
        if (_instance)
        {
            Destroy(gameObject);
            return;
        }
        else
        {
            _instance = this;
        }

        // Generate grid positions
        for (var x = 0; x < gridsSize.x; x++)
        {
            for (var z = 0; z < gridsSize.z; z++)
            {
                var grid = Instantiate(gridPrefab,
                    transform.position + new Vector3(x * (1 + gridSpacing), 0, z * (1 + gridSpacing)),
                    Quaternion.identity, transform);
                gridPositions.Add(grid.transform.position);
            }
        }
    }
}