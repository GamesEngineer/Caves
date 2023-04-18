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

        const float COROUTINE_TIME_SLICE = 0.05f; // seconds

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
            // Move the room's center to the its unique placement in the larger grid of rooms
            center.x += RoomSize.x * (roomNumber % mazeWidth);
            center.y = 0;
            center.z += RoomSize.z * (roomNumber / mazeWidth);

            // TODO - Add random offset to center

            roomCenters[roomNumber] = center;

            HashSet<GridWall> wallsOfRoom = new();
            List<GridWall> wallsAdded = new();
            List<GridWall> wallsRemoved = new();

            if (!TryExcavateStandingSpace(center, wallsAdded, wallsRemoved))
            {
                yield break;
            }

            UpdateSetOfWalls(wallsOfRoom, wallsAdded, wallsRemoved);

            float time = Time.realtimeSinceStartup;
            // Randomly excavate into walls of the room until we make the room "big enough"
            int maxCellCount = RoomSize.x * RoomSize.y * RoomSize.z;
            for (int i = 0; i < maxCellCount; i++)
            {
                if (!wallsOfRoom.GetRandomItem(out GridWall wall, out _)) break;

                TryExcavateWall(wall, wallsAdded, wallsRemoved);

                UpdateSetOfWalls(wallsOfRoom, wallsAdded, wallsRemoved);

                // Periodically give control back to Unity's update loop, so
                // that the app remains responsive.
                if (Time.realtimeSinceStartup - time > COROUTINE_TIME_SLICE)
                {
                    time = Time.realtimeSinceStartup;
                    yield return null;
                }
            }

            print($"allWalls.Count={allWalls.Count}");

            OnCreated?.Invoke();
        }

        private void UpdateSetOfWalls(HashSet<GridWall> wallSet,
            ICollection<GridWall> wallsAdded, ICollection<GridWall> wallsRemoved)
        {
            wallSet.UnionWith(wallsAdded);
            wallSet.ExceptWith(wallsRemoved);
            wallsAdded.Clear();
            wallsRemoved.Clear();
        }

        private bool TryExcavateWall(GridWall wall,
            ICollection<GridWall> wallsAdded, ICollection<GridWall> wallsRemoved)
        {
            bool okay;
            okay = TryExcavateStandingSpace(wall.PositiveSide, wallsAdded, wallsRemoved);
            okay |= TryExcavateStandingSpace(wall.NegativeSide, wallsAdded, wallsRemoved);
            return okay;
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

    static class Extensions
    {
        public static bool GetRandomItem<ItemType>(this IReadOnlyCollection<ItemType> items,
            out ItemType randomItem, out int itemIndex)
        {
            itemIndex = -1;
            randomItem = default;
            if (items.Count <= 0) return false;
            itemIndex = UnityEngine.Random.Range(0, items.Count);
            randomItem = items.ElementAt(itemIndex);
            return true;
        }
    }
}
