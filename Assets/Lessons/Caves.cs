using GameU;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lessons
{
    public class Caves : MonoBehaviour
    {
        [SerializeField]
        Vector3Int gridSize = new(64, 8, 64);

        [SerializeField, Range(1, 10)]
        int mazeWidth = 4;

        [SerializeField]
        bool createMaze;

        [SerializeField, Tooltip("0 = randomize")]
        int seed = 0;

        [SerializeField, Range(0f, 1f), Tooltip("Ratio of room size to use for random offsets of rooms")]
        float randomRoomCenters = 0.5f;

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

            if (seed == 0) seed = (int)DateTime.Now.Ticks;
            UnityEngine.Random.InitState(seed);
        }

        private void Start()
        {
            if (createMaze)
            {
                StartCoroutine(CreateTestMaze());
            }
            else
            {
                StartCoroutine(ExcavateCaves());
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

        private IEnumerator ExcavateCaves()
        {
            // TODO - initialize progress meter
            yield return null;

            // Excavate all of the rooms (use RoomCount to know how many rooms)
            for (int roomNumber = 0; roomNumber < RoomCount; roomNumber++)
            {
                yield return ExcavateRoom(roomNumber);
            }

            // TODO - set progress meter to 100%
            yield return null;

            // Generate a maze that connects rooms
            GridMaze maze = new(new Vector3Int(mazeWidth, 1, mazeWidth));
            maze.Generate();

            // TODO - Lesson 9 - Excavate passages between connected rooms
            // Visit each room and excavate a passage to its west and south
            // neighbor rooms, only if there is no maze wall between them.
            for (int fromRoomIndex = 0; fromRoomIndex < RoomCount; fromRoomIndex++)            
            {
                // TODO - update progress meter

                int x = fromRoomIndex % mazeWidth;
                int z = fromRoomIndex / mazeWidth;
                Vector3Int fromRoomCoords = new(x, 0, z);

                GridWall westWall = new(fromRoomCoords, FaceAxis.WestEast);
                if (!maze.Walls.Contains(westWall))
                {
                    Vector3Int westRoomCoords = fromRoomCoords.Step(Direction.West);
                    int westRoomIndex = westRoomCoords.x + westRoomCoords.z * mazeWidth;
                    yield return ExcavatePassageBetweenRoomCenters(fromRoomIndex, westRoomIndex);
                }

                GridWall southWall = new(fromRoomCoords, FaceAxis.SouthNorth);
                if (!maze.Walls.Contains(southWall))
                {
                    Vector3Int southRoomCoords = fromRoomCoords.Step(Direction.South);
                    int southRoomIndex = southRoomCoords.x + southRoomCoords.z * mazeWidth;
                    yield return ExcavatePassageBetweenRoomCenters(fromRoomIndex, southRoomIndex);
                }
            }

            // TODO - set progress meter to 100%
            yield return null;

            OnCreated?.Invoke();
        }

        // TODO - Lesson 9 - Excavate passage with a grid walk
        // TODO - Lesson 10 - Excavate passage with a constrained random walk
        private IEnumerator ExcavatePassageBetweenRoomCenters(int roomA, int roomB)
        {
            Vector3Int a = roomCenters[roomA];
            Vector3Int b = roomCenters[roomB];
            a = FindFloor(a);
            b = FindFloor(b);
            yield return ExcavatePassage(a, b);
        }

        public Vector3Int FindFloor(Vector3Int coordinates)
        {
            Vector3Int sample = coordinates.Step(Direction.Down);
            while (IsCellOpen(sample))
            {
                coordinates = sample;
                sample = sample.Step(Direction.Down);
            }
            return coordinates;
        }

        private IEnumerator ExcavatePassage(Vector3Int fromCoordinates, Vector3Int toCoordinates)
        {
            float time = Time.realtimeSinceStartup;

            Vector3Int c = fromCoordinates;
            TryExcavateStandingSpace(c);
            int failSafe = (gridSize.x + gridSize.y + gridSize.z) * 10;
            int passageLength = 0;
            while (c.x != toCoordinates.x || c.z != toCoordinates.z)
            {
                if (passageLength > failSafe)
                {
                    Debug.LogError("Passage length too long");
                    yield break;
                }

                c = c.LateralStepTowards(toCoordinates);
                TryExcavateStandingSpace(c);
                passageLength++;

                // TIME SLICE
                // Periodically give control back to Unity's update loop,
                // so that the app remains interactive and avoid freezing.
                if (Time.realtimeSinceStartup - time > COROUTINE_TIME_SLICE)
                {
                    time = Time.realtimeSinceStartup;
                    yield return null;
                }
            }
        }

        private IEnumerator ExcavateRoom(int roomNumber)
        {
            Vector3Int halfRoomSize = Vector3Int.Max(RoomSize / 2, Vector3Int.one);
            Vector3Int center = halfRoomSize;
            // Move the room's center to the its unique placement in the larger grid of rooms
            center.x += RoomSize.x * (roomNumber % mazeWidth);
            center.y = 0;
            center.z += RoomSize.z * (roomNumber / mazeWidth);

            if (randomRoomCenters > 0f)
            {
                Vector3Int wiggleRoom = new(
                    (int)(RoomSize.x * randomRoomCenters),
                    (int)(RoomSize.y * randomRoomCenters),
                    (int)(RoomSize.z * randomRoomCenters));
                Vector3Int minInclusive = center - wiggleRoom;
                Vector3Int maxExclusive = center + wiggleRoom + Vector3Int.one;
                minInclusive.Clamp(Vector3Int.zero, gridSize); // TODO is this correct?
                maxExclusive.Clamp(Vector3Int.zero, gridSize);
                center = new Vector3Int(
                    UnityEngine.Random.Range(minInclusive.x, maxExclusive.x),
                    UnityEngine.Random.Range(minInclusive.y, maxExclusive.y),
                    UnityEngine.Random.Range(minInclusive.z, maxExclusive.z));
            }

            roomCenters[roomNumber] = center;

            HashSet<GridWall> wallsOfRoom = new();
            List<GridWall> wallsAdded = new();
            List<GridWall> wallsRemoved = new();

            if (!TryExcavateStandingSpace(center, wallsAdded, wallsRemoved))
            {
                yield break;
            }

            UpdateSetOfWalls(wallsOfRoom, wallsAdded, wallsRemoved);

            // Randomly excavate into walls of the room until we make the room "big enough"
            int maxCellCount = RoomSize.x * RoomSize.y * RoomSize.z;
            maxCellCount /= 2;
            yield return ExpandRoom(wallsOfRoom, maxCellCount, wallsAdded, wallsRemoved);

            print($"allWalls.Count={allWalls.Count}");

        }

        private IEnumerator ExpandRoom(HashSet<GridWall> wallsOfRoom, int maxCellCount,
            ICollection<GridWall> wallsAdded, ICollection<GridWall> wallsRemoved)
        {
            float time = Time.realtimeSinceStartup;
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
