using System.Collections.Generic;
using Mask;
using UnityEngine;

namespace Controller
{
    public class NPC : GridMover
    {
        // Light Settings
        private const int LightRangeForward = 1;
        private const int LightRangeBack = 1;
        private const int LightRangeLeft = 1;
        private const int LightRangeRight = 1;

        public List<Vector3> GetLightTiles()
        {
            // Get Light Settings from Mask if equipped
            var mask = GetComponentInChildren<AnimalMask>();
            var lightRangeForward = mask ? mask.lightRangeForward : LightRangeForward;
            var lightRangeBack = mask ? mask.lightRangeBack : LightRangeBack;
            var lightRangeLeft = mask ? mask.lightRangeLeft : LightRangeLeft;
            var lightRangeRight = mask ? mask.lightRangeRight : LightRangeRight;

            // Calculate lighted tiles
            var tiles = new List<Vector3>();
            var center = transform.position;
            for (var x = -lightRangeLeft; x <= lightRangeRight; x++)
            for (var z = -lightRangeBack; z <= lightRangeForward; z++)
                tiles.Add(new Vector3(center.x + x, center.y, center.z + z));
            return tiles;
        }

        public AnimalMask GetEquippedMask()
        {
            return GetComponentInChildren<AnimalMask>();
        }
    }
}