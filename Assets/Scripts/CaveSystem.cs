using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameU
{
    internal static class ExtensionMethods
    {
        public static bool GetRandomItem<ItemType>(this IReadOnlyCollection<ItemType> items, out ItemType randomItem, out int itemIndex)
        {
            itemIndex = -1;
            randomItem = default;
            if (items.Count <= 0) return false;
            itemIndex = UnityEngine.Random.Range(0, items.Count);
            var iterator = items.GetEnumerator();
            iterator.MoveNext();
            for (int i = 0; i < itemIndex; i++)
            {
                iterator.MoveNext();
            }
            randomItem = iterator.Current;
            return true;
        }
    }

    public class CaveSystem : MonoBehaviour
    {
        [SerializeField] Vector3Int gridSize = new(64, 8, 64);
        [SerializeField, Range(1, 7)] int mazeWidth = 4;
        [SerializeField, Range(0f, 1f)] float randomRoomCenters = 0.5f;
        [SerializeField] bool windingPassages = true;
        [SerializeField, Tooltip("0 = randomize")] int seed = 0;
        [SerializeField] bool createMaze;
        [SerializeField] UnityEngine.UI.Image progressImage;

        public int RoomCount => mazeWidth * mazeWidth;
        public Vector3Int GridSize => gridSize;
        public GridMaze RoomsMaze { get; private set; }
        public IReadOnlyCollection<GridWall> Walls => allWalls;

        public event Action OnCreated;
        
        private bool[/*X*/,/*Y*/,/*Z*/] cells; // false = closed, true = open
        private Vector3Int[] roomCenters; // "center" position of each room
        private readonly HashSet<GridWall> allWalls = new();
        private const float COROUTINE_TIME_SLICE = 0.05f; // seconds

        public Vector3Int GetRoomCenter(int roomIndex) => roomCenters[roomIndex];

        public static Vector3Int GetRandomCell(Vector3Int min, Vector3Int max)
        {
            if (min.x > max.x) (min.x, max.x) = (max.x, min.x);
            if (min.y > max.y) (min.y, max.y) = (max.y, min.y);
            if (min.z > max.z) (min.z, max.z) = (max.z, min.z);
            Vector3Int coordinates = new(
                UnityEngine.Random.Range(min.x, max.x),
                UnityEngine.Random.Range(min.y, max.y),
                UnityEngine.Random.Range(min.z, max.z));
            return coordinates;
        }

        public bool IsCellOpen(Vector3Int coordinates)
        {
            if (!coordinates.IsInRange(Vector3Int.zero, gridSize)) return false;
            return cells[coordinates.x, coordinates.y, coordinates.z];
        }

        public bool IsStandingSpaceOpen(Vector3Int coordinates) => IsCellOpen(coordinates) && IsCellOpen(coordinates.Step(Direction.Up));

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

        public int FindFloorHeight(int x, int z)
        {
            Vector3Int sample = new(x, 0, z);
            while (sample.y < GridSize.y && !IsCellOpen(sample))
            {
                sample = sample.Step(Direction.Up);
            }
            return sample.y;
        }

        private void SetWallState(Vector3Int coordinates, FaceAxis faceAxis, bool isPresent,
            ICollection<GridWall> wallsAdded = null, ICollection<GridWall> wallsRemoved = null)
        {
            if (!coordinates.IsInRange(Vector3Int.zero, gridSize + Vector3Int.one))
            {
                throw new ArgumentOutOfRangeException();
            }

            var wall = new GridWall(coordinates, faceAxis);

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

        private bool TryExcavateCell(Vector3Int coordinates, ICollection<GridWall> wallsAdded = null, ICollection<GridWall> wallsRemoved = null)
        {
            if (!coordinates.IsInRange(Vector3Int.zero, gridSize))
            {
                return false;
            }

            // Open the new cell
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
        
        private bool TryCloseCell(Vector3Int coordinates, ICollection<GridWall> wallsAdded = null, ICollection<GridWall> wallsRemoved = null)
        {
            if (!coordinates.IsInRange(Vector3Int.zero, gridSize))
            {
                return false;
            }

            // Close the new cell
            cells[coordinates.x, coordinates.y, coordinates.z] = false;

            // Update neighboring walls
            Vector3Int west = coordinates.Step(Direction.West);
            Vector3Int east = coordinates.Step(Direction.East);
            SetWallState(coordinates, FaceAxis.WestEast, IsCellOpen(west), wallsAdded, wallsRemoved);
            SetWallState(east, FaceAxis.WestEast, IsCellOpen(east), wallsAdded, wallsRemoved);

            Vector3Int down = coordinates.Step(Direction.Down);
            Vector3Int up = coordinates.Step(Direction.Up);
            SetWallState(coordinates, FaceAxis.DownUp, IsCellOpen(down), wallsAdded, wallsRemoved);
            SetWallState(up, FaceAxis.DownUp, IsCellOpen(up), wallsAdded, wallsRemoved);

            Vector3Int south = coordinates.Step(Direction.South);
            Vector3Int north = coordinates.Step(Direction.North);
            SetWallState(coordinates, FaceAxis.SouthNorth, IsCellOpen(south), wallsAdded, wallsRemoved);
            SetWallState(north, FaceAxis.SouthNorth, IsCellOpen(north), wallsAdded, wallsRemoved);

            return true;
        }

        private bool TryExcavateStandingSpace(Vector3Int coordinates, ICollection<GridWall> wallsAdded = null, ICollection<GridWall> wallsRemoved = null)
        {
            bool okay = TryExcavateCell(coordinates, wallsAdded, wallsRemoved);
            if (!okay) return false;
            // Open the cell above. This assumes that character height is 2x cell height.
            coordinates = coordinates.Step(Direction.Up);
            TryExcavateCell(coordinates, wallsAdded, wallsRemoved); // it's okay for this to fail
            return true;
        }

        private bool TryExcavateWall(GridWall wall, ICollection<GridWall> wallsAdded = null, ICollection<GridWall> wallsRemoved = null)
        {
            bool okay = TryExcavateStandingSpace(wall.PositiveSide, wallsAdded, wallsRemoved);
            okay |= TryExcavateStandingSpace(wall.NegativeSide, wallsAdded, wallsRemoved);
            return okay;
        }

        private void UpdateSetOfWalls(HashSet<GridWall> wallSet, ICollection<GridWall> wallsAdded, ICollection<GridWall> wallsRemoved)
        {
            wallSet.UnionWith(wallsAdded);
            wallSet.ExceptWith(wallsRemoved);
            wallsAdded.Clear();
            wallsRemoved.Clear();
        }

        private IEnumerator ExcavateVolume(Vector3Int coordinates, int maxCellCount)
        {
            HashSet<GridWall> wallsOfVolume = new();
            List<GridWall> wallsAdded = new();
            List<GridWall> wallsRemoved = new();

            if (!TryExcavateStandingSpace(coordinates, wallsAdded, wallsRemoved))
            {
                yield break; // cannot make a volume at these coordinates
            }
            UpdateSetOfWalls(wallsOfVolume, wallsAdded, wallsRemoved);

            float time = Time.realtimeSinceStartup;
            for (int i = 0; i < maxCellCount; i++)
            {
                if (!wallsOfVolume.GetRandomItem(out GridWall wall, out _)) break;
                TryExcavateWall(wall, wallsAdded, wallsRemoved);
                UpdateSetOfWalls(wallsOfVolume, wallsAdded, wallsRemoved);

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
            Vector3Int blockSize = gridSize / mazeWidth;
            blockSize = Vector3Int.Max(blockSize, Vector3Int.one);
            Vector3Int halfBlockSize = blockSize / 2;

            Vector3Int center = halfBlockSize;
            // move the room's center to the its unique placement in the larger grid of rooms
            center.x += blockSize.x * (roomNumber % mazeWidth);
            center.y = 0;
            center.z += blockSize.z * (roomNumber / mazeWidth);

            Vector3Int roomSize = Vector3Int.Max(blockSize / 2, Vector3Int.one);
            int roomCellCount = 2 * roomSize.x * roomSize.y * roomSize.z;

            if (randomRoomCenters > 0f)
            {
                Vector3Int wiggle = new(
                    (int)(roomSize.x * randomRoomCenters),
                    (int)(roomSize.y * randomRoomCenters),
                    (int)(roomSize.z * randomRoomCenters));
                Vector3Int minInclusive = center - wiggle;
                Vector3Int maxExclusive = center + wiggle + Vector3Int.one;
                minInclusive.Clamp(Vector3Int.zero, gridSize);
                maxExclusive.Clamp(Vector3Int.zero, gridSize);
                center = GetRandomCell(minInclusive, maxExclusive);
            }

            // Lesson - start with just Excavate(center) before implementing ExcavateVolume(center, roomCellCount)
            yield return ExcavateVolume(center, roomCellCount); // TODO - limit excavation to min/max
            roomCenters[roomNumber] = center;
            print($"Room {roomNumber} has {roomCellCount} cells placed at {center} with a max size of {roomSize}");
        }

        private IEnumerator ExcavatePassage(Vector3Int fromCoordinates, Vector3Int toCoordinates)
        {
            Vector3Int c = fromCoordinates;
            TryExcavateStandingSpace(c);
            float time = Time.realtimeSinceStartup;
            int failSafe = gridSize.x + gridSize.y + gridSize.z * 10;
            int passageLength = 0;
            while ((c.x != toCoordinates.x || c.z != toCoordinates.z) &&
                passageLength < failSafe)
            {
                if (windingPassages) // CHALLENGE! implement winding passages on your own
                {                    
                    Direction allowedDirections = Direction.All;
                    // Disallow flying (but climbing out of a hole is okay)
                    if (IsCellOpen(c.Step(Direction.Down))) allowedDirections &= ~Direction.Up;
                    // Disallow digging deep holes
                    if (IsCellOpen(c.Step(Direction.Up, 2))) allowedDirections &= ~Direction.Down;
                    c = c.RandomStepTowards(toCoordinates, allowedDirections);
                }
                else // LESSON - keep it simple and just do lateral passages
                {
                    c = c.LateralStepTowards(toCoordinates);
                }
                // TODO - disallow oscillating between adjacent cells
                c.Clamp(Vector3Int.zero, gridSize - Vector3Int.one * 2);
                TryExcavateStandingSpace(c);

                //FillHole(c);
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
            print($"created passage of length {passageLength}");
        }

        private IEnumerator ExcavatePassageBetweenRoomCenters(int roomA, int roomB)
        {
            Vector3Int a = roomCenters[roomA];
            Vector3Int b = roomCenters[roomB];
            a = FindFloor(a);
            b = FindFloor(b);
            yield return ExcavatePassage(a, b);
        }

        private IEnumerator Excavate()
        {
            progressImage.fillAmount = 0f;
            progressImage.gameObject.SetActive(true);
            yield return null;

            // Excavate rooms

            for (int roomNumber = 0; roomNumber < RoomCount; roomNumber++)
            {
                progressImage.fillAmount = roomNumber / (float)RoomCount;
                yield return ExcavateRoom(roomNumber);
            }

            progressImage.fillAmount = 1f;
            yield return null;

            // Excavate passages between rooms

            // Create an imaginary maze of rooms on a square grid
            // Each cell of the grid represents a room.
            Vector3Int mazeSize = new Vector3Int(mazeWidth, 1, mazeWidth);
            RoomsMaze = new GridMaze(mazeSize);
            RoomsMaze.Generate();

            // Visit each room and excavate a passage to its west and south
            // neighbor rooms, unless there is a maze wall between them.
            for (int currentRoomIndex = 0; currentRoomIndex < RoomCount; currentRoomIndex++)
            {
                progressImage.fillAmount = currentRoomIndex / (float)RoomCount;

                // Get the room's location in maze coordinates.
                // (NOT the same as the cave's voxel coordinates)
                int x = currentRoomIndex % mazeWidth;
                int z = currentRoomIndex / mazeWidth;
                Vector3Int fromRoomCoords = new Vector3Int(x, 0, z);

                GridWall westWall = new(fromRoomCoords, FaceAxis.WestEast);
                if (!RoomsMaze.Walls.Contains(westWall))
                {
                    // Get the index of the room to the west and excavate to it from this room
                    Vector3Int west = fromRoomCoords.Step(Direction.West);
                    int westRoomIndex = west.x + west.z * mazeWidth;
                    yield return ExcavatePassageBetweenRoomCenters(currentRoomIndex, westRoomIndex);
                }

                GridWall southWall = new(fromRoomCoords, FaceAxis.SouthNorth);
                if (!RoomsMaze.Walls.Contains(southWall))
                {
                    // Get the index of the room to the south and excavate to it from this room
                    Vector3Int south = fromRoomCoords.Step(Direction.South);
                    int southRoomIndex = south.x + south.z * mazeWidth;
                    yield return ExcavatePassageBetweenRoomCenters(currentRoomIndex, southRoomIndex);
                }
            }

            progressImage.color = Color.white;
            progressImage.fillAmount = 1f;
            yield return null;

            OnCreated?.Invoke();
        }

        public void FillHole(Vector3Int coordinates)
        {
            Vector3Int c = coordinates;
            c.y = -1;
            while (c.y < coordinates.y && c.y < gridSize.y)
            {
                TryCloseCell(c);
                c = c.Step(Direction.Up);
            }
        }

        private void Awake()
        {
            cells = new bool[gridSize.x, gridSize.y, gridSize.z];
            roomCenters = new Vector3Int[RoomCount];
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
                StartCoroutine(Excavate());
            }
        }

        private IEnumerator CreateTestMaze()
        {
            gridSize.y = 1;
            float time = Time.realtimeSinceStartup;
            RoomsMaze = new(gridSize);
            RoomsMaze.Generate();
            print($"Maze generated {RoomsMaze.Walls.Count} walls in {Time.realtimeSinceStartup - time} seconds");
            yield return null;

            allWalls.UnionWith(RoomsMaze.Walls.Where(w => w.faceAxis != FaceAxis.DownUp));
            print($"{allWalls.Count} walls remaining");
            yield return null;

            OnCreated?.Invoke();
        }
    }
}
