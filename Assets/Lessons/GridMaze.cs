using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Lessons
{
    public class GridMaze
    {
        // This maze generator uses the "randomized Prim's algorithm"
        // https://weblog.jamisbuck.org/2011/1/10/maze-generation-prim-s-algorithm.html
        // https://en.wikipedia.org/wiki/Maze_generation_algorithm#Randomized_Prim's_algorithm
        // TODO - Reduce bias by providing an alternate generation method that uses frontier cells, instead of frontier walls.

        public Vector3Int GridSize { get; private set; }
        public IReadOnlyCollection<GridWall> Walls => allWalls;

        public GridMaze(Vector3Int gridSize)
        {
            GridSize = gridSize;
            cells = new bool[gridSize.x, gridSize.y, gridSize.z];
        }

        public void Generate()
        {
            Vector3Int startPoint = GetRandomCellCoordinates();
            OpenCell(startPoint);
            while (TakeRandomWallFromFrontier(out GridWall wall))
            {
                // Try to open the cells on both sides of the frontier wall,
                // and remove the wall if either side was newly opened.
                if (OpenCell(wall.PositiveSide) | OpenCell(wall.NegativeSide))
                {
                    allWalls.Remove(wall);
                }
            }
        }

        public Vector3Int GetRandomCellCoordinates() =>
            new(Random.Range(0, GridSize.x),
                Random.Range(0, GridSize.y),
                Random.Range(0, GridSize.z));

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

        /// <summary>
        /// Returns a sequence of coordinates for each adjacent neighboring cell that is accesible (i.e., not separated by a wall).
        /// </summary>
        /// <param name="coordinates">the cell's coordinates</param>
        /// <param name="ignoreDirections">flags that specify which adjacent neighbors to skip</param>
        /// <returns></returns>
        public IEnumerator<Vector3Int> GetAccesibleNeighbors(Vector3Int coordinates, Direction ignoreDirections = Direction.None)
        {
            if (!coordinates.IsInRange(Vector3Int.zero, GridSize))
            {
                yield break;
            }

            if (!ignoreDirections.HasFlag(Direction.West))
            {
                Vector3Int west = coordinates.Step(Direction.West);
                GridWall westWall = new GridWall(coordinates, FaceAxis.WestEast);
                if (allWalls.Contains(westWall)) yield return west;
            }

            if (!ignoreDirections.HasFlag(Direction.East))
            {
                Vector3Int east = coordinates.Step(Direction.East);
                GridWall eastWall = new GridWall(east, FaceAxis.WestEast);
                if (allWalls.Contains(eastWall)) yield return east;
            }

            if (!ignoreDirections.HasFlag(Direction.South))
            {
                Vector3Int south = coordinates.Step(Direction.South);
                GridWall southWall = new GridWall(coordinates, FaceAxis.SouthNorth);
                if (allWalls.Contains(southWall)) yield return south;
            }

            if (!ignoreDirections.HasFlag(Direction.North))
            {
                Vector3Int north = coordinates.Step(Direction.North);
                GridWall northWall = new GridWall(north, FaceAxis.SouthNorth);
                if (allWalls.Contains(northWall)) yield return north;
            }

            if (!ignoreDirections.HasFlag(Direction.Down))
            {
                Vector3Int down = coordinates.Step(Direction.Down);
                GridWall downWall = new GridWall(coordinates, FaceAxis.DownUp);
                if (allWalls.Contains(downWall)) yield return down;
            }

            if (!ignoreDirections.HasFlag(Direction.Up))
            {
                Vector3Int up = coordinates.Step(Direction.Up);
                GridWall upWall = new GridWall(up, FaceAxis.DownUp);
                if (allWalls.Contains(upWall)) yield return up;
            }
        }

        #region private stuff

        private readonly bool[/*X*/,/*Y*/,/*Z*/] cells; // false = closed, true = open
        private readonly HashSet<GridWall> allWalls = new();
        private readonly List<GridWall> frontierWalls = new(); // interior walls to be considered for removal

        private bool IsCellOpen(Vector3Int coordinates) =>
            coordinates.IsInRange(Vector3Int.zero, GridSize) &&
            cells[coordinates.x, coordinates.y, coordinates.z];

        private bool TakeRandomWallFromFrontier(out GridWall wall)
        {
            if (frontierWalls.Count == 0)
            {
                wall = default;
                return false;
            }

            int r = Random.Range(0, frontierWalls.Count);
            wall = frontierWalls[r];
            frontierWalls.RemoveAt(r);
            return true;
        }

        private bool OpenCell(Vector3Int coordinates)
        {
            // Can't open cells that are outside of the grid
            if (!coordinates.IsInRange(Vector3Int.zero, GridSize))
            {
                return false;
            }

            // Can't open cells that were already opened
            if (cells[coordinates.x, coordinates.y, coordinates.z])
            {
                return false;
            }

            // Open the cell
            cells[coordinates.x, coordinates.y, coordinates.z] = true;

            // Add the cell's neighboring walls
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
#if DEBUG
            if (!coordinates.IsInRange(Vector3Int.zero, GridSize + Vector3Int.one))
            {
                throw new ArgumentOutOfRangeException();
            }
#endif

            GridWall wall = new(coordinates, faceAxis);
            allWalls.Add(wall);

            // Ignore exterior walls for the frontier, since they can never be removed.
            if (!IsExteriorWall(wall))
            {
                Debug.Assert(!frontierWalls.Contains(wall));
                frontierWalls.Add(wall);
            }
        }

        #endregion
    }
}
