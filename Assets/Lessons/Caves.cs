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
        private bool[/*X*/,/*Y*/,/*Z*/] cells; // false = closed, true = open

        public bool IsCellOpen(Vector3Int coordinates)
        {
            if (!coordinates.IsInRange(Vector3Int.zero, gridSize)) return false;
            return cells[coordinates.x, coordinates.y, coordinates.z];
        }

        public Vector3Int RoomSize { get; private set; }
        public int RoomCount => mazeWidth * mazeWidth;

        private void Awake()
        {
            RoomSize = Vector3Int.Max(gridSize / mazeWidth, Vector3Int.one);
            roomCenters = new Vector3Int[RoomCount];
            cells = new bool[gridSize.x, gridSize.y, gridSize.z];
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

            //********** START HERE 04/11/2023 *********
            // TODO - update wallsOfRoom with wallsAdded and wallsRemoved

            // randomly excavate into walls of the room until we make the room "big enough"
            int maxCellCount = RoomSize.x * RoomSize.y * RoomSize.z;
            for (int i = 0; i < maxCellCount; i++)
            {
                // TODO - get a random wall
                // TODO - excavate through that wall
                // TODO - update wallsOfRoom with wallsAdded and wallsRemoved
            }

            print($"allWalls.Count={allWalls.Count}");

            OnCreated?.Invoke();
        }

        private bool TryExcavateStandingSpace(Vector3Int coordinates,
            ICollection<GridWall> wallsAdded = null,
            ICollection<GridWall> wallsRemoved = null)
        {
            bool okay = TryExcavateCell(coordinates, wallsAdded, wallsRemoved);
            if (!okay) return false;

            // Open the cell above. This assumes that character height is 2x cell height.
            coordinates = coordinates.Step(Direction.Up);
            TryExcavateCell(coordinates, wallsAdded, wallsRemoved); // it's okay for this to fail
            return true;
        }

        private bool TryExcavateCell(Vector3Int coordinates,
            ICollection<GridWall> wallsAdded = null,
            ICollection<GridWall> wallsRemoved = null)
        {
            if (!coordinates.IsInRange(Vector3Int.zero, gridSize)) return false;

            // Open the cell
            cells[coordinates.x, coordinates.y, coordinates.z] = true;

            // Update neighboring walls
            Vector3Int west = coordinates.Step(Direction.West);
            Vector3Int east = coordinates.Step(Direction.East);
            SetWallState(coordinates, FaceAxis.WestEast, !IsCellOpen(west), wallsAdded, wallsRemoved);
            SetWallState(east, FaceAxis.WestEast, !IsCellOpen(east), wallsAdded, wallsRemoved);

            Vector3Int down = coordinates.Step(Direction.Down);
            Vector3Int up = coordinates.Step(Direction.Up);
            SetWallState(coordinates, FaceAxis.DownUp, !IsCellOpen(down), wallsAdded, wallsRemoved);
            SetWallState(up, FaceAxis.DownUp, !IsCellOpen(up), wallsAdded, wallsRemoved);

            Vector3Int south = coordinates.Step(Direction.South);
            Vector3Int north = coordinates.Step(Direction.North);
            SetWallState(coordinates, FaceAxis.SouthNorth, !IsCellOpen(south), wallsAdded, wallsRemoved);
            SetWallState(north, FaceAxis.SouthNorth, !IsCellOpen(north), wallsAdded, wallsRemoved);

            return true;
        }

        private void SetWallState(
            Vector3Int coordinates,
            FaceAxis faceAxis,
            bool isPresent,
            ICollection<GridWall> wallsAdded = null,
            ICollection<GridWall> wallsRemoved = null)
        {
            Debug.Assert(coordinates.IsInRange(Vector3Int.zero, gridSize + Vector3Int.one));

            GridWall wall = new(coordinates, faceAxis);

            if (isPresent)
            {
                allWalls.Add(wall);
                wallsAdded?.Add(wall);
            }
            else
            {
                allWalls.Remove(wall);
                wallsRemoved?.Add(wall);
            }
        }

    }
}
