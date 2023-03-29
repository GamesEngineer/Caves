using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lessons
{
    public class Caves : MonoBehaviour
    {
        [SerializeField] Vector3Int gridSize = new(64, 8, 64);
        [SerializeField, Range(1, 10)] int mazeWidth = 4;
        [SerializeField] bool createMaze;
        public IReadOnlyCollection<GridWall> Walls => allWalls;
        private readonly HashSet<GridWall> allWalls = new();

        public event Action OnCreated;
        private Vector3Int[] roomCenters;

        // TODO - keep track of the state of voxels (aka "cells")

        // TODO - public bool IsCellOpen(Vector3Int coordinates)

        public Vector3Int RoomSize { get; private set; }
        public int RoomCount => mazeWidth * mazeWidth;

        private void Awake()
        {
            RoomSize = Vector3Int.Max(gridSize / mazeWidth, Vector3Int.one);
            roomCenters = new Vector3Int[RoomCount];
        }

        private void Start()
        {
            if (createMaze)
            {
                StartCoroutine(CreateTestMaze());
            }
            else
            {
                StartCoroutine(ExcavateRoom(0));
            }
        }

        private IEnumerator CreateTestMaze()
        {
            GridMaze maze = new(new Vector3Int(mazeWidth, 1, mazeWidth));
            maze.Generate();
            yield return null;

            var onlyVerticalWalls = maze.Walls.Where(wall => wall.faceAxis != FaceAxis.DownUp);
            allWalls.UnionWith(onlyVerticalWalls);
            yield return null;

            OnCreated?.Invoke();
        }

        private IEnumerator ExcavateRoom(int roomNumber)
        {
            Vector3Int halfRoomSize = Vector3Int.Max(RoomSize / 2, Vector3Int.one);
            Vector3Int center = halfRoomSize;
            // move the room's center to the its unique placement in the larger grid of rooms
            center.x += RoomSize.x * (roomNumber % mazeWidth);
            center.y = 0;
            center.z += RoomSize.z * (roomNumber / mazeWidth);

            // TODO - add random offset to center

            roomCenters[roomNumber] = center;

            HashSet<GridWall> wallsOfRoom = new();
            List<GridWall> wallsAdded = new();
            List<GridWall> wallsRemoved = new();

            Vector3Int coordinates = center;

            if (!TryExcavateStandingSpace(coordinates, wallsAdded, wallsRemoved))
            {
                yield break;
            }

            // TODO - update wallsOfRoom with wallsAdded and wallsRemoved

            // TODO - randomly excavate into walls of the room until we make the room "big enough"
        }

        private bool TryExcavateStandingSpace(Vector3Int coordinates,
            ICollection<GridWall> wallsAdded = null,
            ICollection<GridWall> wallsRemoved = null)
        {
            bool okay = TryExcavateCell(coordinates, wallsAdded, wallsRemoved);
            if (!okay) return false;

            // Open the cell above. This assumes that character height is 2x cell height.
            coordinates.Step(Direction.Up);
            TryExcavateCell(coordinates, wallsAdded, wallsRemoved); // it's okay for this to fail
            return true;
        }

        private bool TryExcavateCell(Vector3Int coordinates,
            ICollection<GridWall> wallsAdded = null,
            ICollection<GridWall> wallsRemoved = null)
        {
            //**********************************
            // TODO - START HERE 2023/04/04
            //**********************************
            return false;
        }

        // TODO - private void SetWallState(
        //      Vector3Int coordinates,
        //      FaceAxis faceAxis,
        //      bool isPresent,
        //      ICollection<GridWall> wallsAdded = null,
        //      ICollection<GridWall> wallsRemoved = null)

    }
}
