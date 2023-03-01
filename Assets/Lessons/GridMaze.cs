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

        public Vector3Int GridSize { get; private set; }

        public IReadOnlyCollection<GridWall> Walls => allWalls;

        private readonly HashSet<GridWall> allWalls = new();
        private readonly List<GridWall> frontierWalls = new();
        private readonly bool[/*X*/,/*Y*/,/*Z*/] cells; // false is "closed", true is "open"

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
                if (OpenCell(wall.PositiveSide) | OpenCell(wall.NegativeSide))
                {
                    allWalls.Remove(wall);
                }
            }
        }

        Vector3Int GetRandomCellCoordinates()
        {
            return new Vector3Int(
                Random.Range(0, GridSize.x),
                Random.Range(0, GridSize.y),
                Random.Range(0, GridSize.z));
        }

        bool OpenCell(Vector3Int coordinates)
        {
            if (!coordinates.IsInRange(Vector3Int.zero, GridSize))
            {
                return false;
            }

            // Can't open cell that was already opened
            if (cells[coordinates.x, coordinates.y, coordinates.z])
            {
                return false;
            }

            // Mark the cell as "open"
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

        bool IsCellOpen(Vector3Int coordinates)
        {
            return coordinates.IsInRange(Vector3Int.zero, GridSize) && cells[coordinates.x, coordinates.y, coordinates.z];
        }

        void AddWall(Vector3Int coordinates, FaceAxis faceAxis)
        {
            // TODO - next time!
        }

        bool TakeRandomWallFromFrontier(out GridWall wall)
        {
            if (frontierWalls.Count == 0)
            {
                wall = default;
                return false;
            }

            int randomIndex =  Random.Range(0, frontierWalls.Count);
            wall = frontierWalls[randomIndex];
            frontierWalls.RemoveAt(randomIndex);
            return true;
        }
    }
}
