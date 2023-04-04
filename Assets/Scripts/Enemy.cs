using Lessons;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.UIElements;

namespace GameU
{
    public class Enemy : MonoBehaviour
    {
        public Vector3Int coords;

        Player player;
        CaveWalls caveWalls;

        private void Start()
        {
            player = FindObjectOfType<Player>();
            player.OnStepTaken += StepTowardsPlayer;
            caveWalls = FindObjectOfType<CaveWalls>();
        }

        public void StepTowardsPlayer()
        {
            coords = coords.LateralStepTowards(player.CellCoordinates);
            if (coords == player.CellCoordinates || Vector3Int.Distance(coords, player.CellCoordinates) > 5)
            {
                return;
            }
            Vector3 position;
            caveWalls.TryGetFloorPosition(ref coords, out position);
            transform.position = position;
        }
    }
}