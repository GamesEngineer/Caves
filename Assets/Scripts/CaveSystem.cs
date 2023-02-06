using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
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
        [SerializeField] int roomCount = 4;
        [SerializeField, Range(0f, 1f)] float randomRoomCenters = 0.5f;
        [SerializeField] bool windingPassages = true;
        [SerializeField, Tooltip("0 = randomize")] int seed = 0;
        [SerializeField] bool createMaze;
        [SerializeField] UnityEngine.UI.Image progressImage;

        public int RoomCount => roomCount;
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
            int divisor = Mathf.CeilToInt(Mathf.Sqrt(roomCount));
            Vector3Int blockSize = gridSize / divisor;
            blockSize = Vector3Int.Max(blockSize, Vector3Int.one);
            Vector3Int halfBlockSize = blockSize / 2;

            Vector3Int center = halfBlockSize;
            // move the room's center to the its unique placement in the larger grid of rooms
            center.x += blockSize.x * (roomNumber % divisor);
            center.y = 0;
            center.z += blockSize.z * (roomNumber / divisor);

            Vector3Int roomSize = Vector3Int.Max(blockSize / 2, Vector3Int.one);
            int roomCellCount = 2 * roomSize.x * roomSize.y * roomSize.z;

            if (randomRoomCenters > 0f)
            {
                Vector3Int wiggle = new Vector3Int(
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
            while ((c.x != toCoordinates.x || c.z != toCoordinates.z) && passageLength < failSafe)
            {
                if (windingPassages)
                {
                    Direction allowedDirections = Direction.All;
                    // Disallow jumping out of holes
                    if (IsCellOpen(c.Step(Direction.Down))) allowedDirections &= ~Direction.Up;
                    // Disallow digging deep holes
                    if (IsCellOpen(c.Step(Direction.Up, 2))) allowedDirections &= ~Direction.Down;                    
                    c = c.RandomOrthogonalStepTowards(toCoordinates, allowedDirections);
                }
                else
                {
                    c = c.LateralOrthogonalStepTowards(toCoordinates);
                }
                // TODO - disallow oscillating between adjacent cells
                c.Clamp(Vector3Int.zero, gridSize - Vector3Int.one * 2);
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
            print($"created passage of length {passageLength}");
        }

        private IEnumerator Excavate()
        {
            progressImage.fillAmount = 0f;
            progressImage.gameObject.SetActive(true);
            yield return null;

            for (int roomNumber = 0; roomNumber < roomCount; roomNumber++)
            {
                progressImage.fillAmount = roomNumber / (float)roomCount;
                yield return ExcavateRoom(roomNumber);
            }

            progressImage.fillAmount = 1f;
            yield return null;

            // excavate passages between rooms with a maze algorithm
            int width = Mathf.CeilToInt(Mathf.Sqrt(roomCount));
            Vector3Int mazeSize = new(width, 1, width);
            RoomsMaze = new(mazeSize);
            RoomsMaze.Generate();

            for (int z = 0; z < width; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3Int fromRoomCoords = new(x, 0, z);
                    int fromRoom = x + z * width;
                    if (fromRoom >= roomCount)
                    {
                        continue;
                    }

                    progressImage.fillAmount = fromRoom / (float)roomCount;

                    Vector3Int west = fromRoomCoords.Step(Direction.West);
                    Vector3Int south = fromRoomCoords.Step(Direction.South);
                    GridWall westWall = new GridWall(fromRoomCoords, FaceAxis.WestEast);
                    GridWall southWall = new GridWall(fromRoomCoords, FaceAxis.SouthNorth);

                    if (!RoomsMaze.Walls.Contains(westWall))
                    {
                        int westRoom = west.x + west.z * width;
                        if (westRoom < 0)
                        {
                            continue;
                        }
                        Vector3Int a = roomCenters[fromRoom];
                        Vector3Int b = roomCenters[westRoom];
                        a = FindFloor(a);
                        b = FindFloor(b);
                        yield return ExcavatePassage(a, b);
                    }                  
                    
                    if (!RoomsMaze.Walls.Contains(southWall))
                    {
                        int southRoom = south.x + south.z * width;
                        if (southRoom < 0)
                        {
                            continue;
                        }
                        Vector3Int a = roomCenters[fromRoom];
                        Vector3Int b = roomCenters[southRoom];
                        a = FindFloor(a);
                        b = FindFloor(b);
                        yield return ExcavatePassage(a, b);
                    }                   
                }
            }

            progressImage.color = Color.white;
            progressImage.fillAmount = 1f;
            yield return null;

            OnCreated?.Invoke();
        }

        private void Awake()
        {
            cells = new bool[gridSize.x, gridSize.y, gridSize.z];
            roomCenters = new Vector3Int[roomCount];
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
