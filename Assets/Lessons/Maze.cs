using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Lesson
{
    public class Maze
    {
        // This maze generator uses the "randomized Prim's algorithm"
        // https://weblog.jamisbuck.org/2011/1/10/maze-generation-prim-s-algorithm.html
        // https://en.wikipedia.org/wiki/Maze_generation_algorithm#Randomized_Prim's_algorithm

        public Vector3Int GridSize { get; private set; }
        public bool[,,] Cells { get; private set; }
        public HashSet<GridWall> Walls { get; private set; }

        private readonly List<GridWall> frontierWalls;

        public Maze(Vector3Int gridSize)
        {
            GridSize = gridSize;
            Cells = new bool[gridSize.x, gridSize.y, gridSize.z]; // false = closed, true is open
            Walls = new HashSet<GridWall>();
            frontierWalls = new List<GridWall>();

            // Choose a start point
            Vector3Int startPoint = Vector3Int.zero;
            // Open the cell at the start point (adding its walls to the frontier)
            OpenCell(startPoint);

            // While we can take a random wall from the frontier...
            while (TakeRandomWallFromFrontier(out GridWall wall))
            {
                // Attempt to open cells of both sides of the wall
                // if either side was newly opened, then remove the wall
                if (OpenCell(wall.PositiveSide) | OpenCell(wall.NegativeSide))
                {
                    Walls.Remove(wall);
                }
            }
        }

        private bool TakeRandomWallFromFrontier(out GridWall wall)
        {
            // TODO - finish implementing
            wall = default;
            return false;
        }

        public bool IsCellOpen(Vector3Int coordinates) =>
            coordinates.IsInRange(Vector3Int.zero, GridSize) &&
            Cells[coordinates.x, coordinates.y, coordinates.z];

        private bool OpenCell(Vector3Int coordinates)
        {
            // Can't open cells that are outside of the grid
            // TODO - finish implementing

            // Can't open cells that are already opened
            // TODO - finish implementing

            // Open cell
            Cells[coordinates.x, coordinates.y, coordinates.z] = true;

            // Add the cells neighboring walls
            Vector3Int west = coordinates.Step(Direction.West);
            Vector3Int east = coordinates.Step(Direction.East);
            Vector3Int down = coordinates.Step(Direction.Down);
            Vector3Int up = coordinates.Step(Direction.Up);
            Vector3Int south = coordinates.Step(Direction.South);
            Vector3Int north = coordinates.Step(Direction.North);

            if (!IsCellOpen(west)) AddWall(coordinates, FaceAxis.WestEast);
            if (!IsCellOpen(east)) AddWall(east, FaceAxis.WestEast);

            if (!IsCellOpen(down)) AddWall(coordinates, FaceAxis.DownUp);
            if (!IsCellOpen(up)) AddWall(up, FaceAxis.DownUp);

            if (!IsCellOpen(south)) AddWall(coordinates, FaceAxis.SouthNorth);
            if (!IsCellOpen(north)) AddWall(north, FaceAxis.SouthNorth);

            return true;
        }

        private void AddWall(Vector3Int coordinates, FaceAxis faceAxis)
        {
            GridWall wall = new GridWall(coordinates, faceAxis);
            Walls.Add(wall);
            if (!IsExteriorWall(wall))
            {
                frontierWalls.Add(wall);
            }
        }

        public bool IsExteriorWall(GridWall wall)
        {
            if (wall.coordinates.x == 0 && wall.faceAxis == FaceAxis.WestEast) return true;
            if (wall.coordinates.x == GridSize.x && wall.faceAxis == FaceAxis.WestEast) return true;

            if (wall.coordinates.y == 0 && wall.faceAxis == FaceAxis.DownUp) return true;
            if (wall.coordinates.y == GridSize.y && wall.faceAxis == FaceAxis.DownUp) return true;

            if (wall.coordinates.z == 0 && wall.faceAxis == FaceAxis.SouthNorth) return true;
            if (wall.coordinates.z == GridSize.z && wall.faceAxis == FaceAxis.SouthNorth) return true;

            return false;
        }
    }
}
