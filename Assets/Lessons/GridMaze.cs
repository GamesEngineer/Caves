using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Lessons
{
    public class GridMaze
    {
        // TODO - implement GridMaze
        // This maze generator uses the "randomized Prim's algorithm"
        // https://weblog.jamisbuck.org/2011/1/10/maze-generation-prim-s-algorithm.html
        // https://en.wikipedia.org/wiki/Maze_generation_algorithm#Randomized_Prim's_algorithm

        public Vector3Int GridSize { get; private set; }

        public IReadOnlyCollection<GridWall> Walls;

        private readonly HashSet<GridWall> allWalls = new();

        // TODO - start here next time
    }
}
