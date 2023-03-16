using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lessons
{
    public class Caves : MonoBehaviour
    {
        [SerializeField, Range(1, 10)] int mazeWidth = 4;
        [SerializeField] bool createMaze;
        public IReadOnlyCollection<GridWall> Walls => allWalls;
        private readonly HashSet<GridWall> allWalls = new();

        public event Action OnCreated;

        void Start()
        {
            if (createMaze)
            {
                StartCoroutine(CreateTestMaze());
            }
            else
            {
                // TODO - excavate caves
            }
        }

        IEnumerator CreateTestMaze()
        {
            GridMaze maze = new(new Vector3Int(mazeWidth, 1, mazeWidth));
            maze.Generate();
            yield return null;

            var onlyVerticalWalls = maze.Walls.Where(wall => wall.faceAxis != FaceAxis.DownUp);
            allWalls.UnionWith(onlyVerticalWalls);
            yield return null;

            OnCreated?.Invoke();
        }
    }
}
