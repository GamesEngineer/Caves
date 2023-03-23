using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

namespace Lessons
{
    [Flags]
    public enum Direction : byte
    {
        None = 0,

        West    = 0b_00000001,
        Down    = 0b_00000010,
        South   = 0b_00000100,

        East    = 0b_00001000,
        Up      = 0b_00010000,
        North   = 0b_00100000,

        Negative = Direction.West | Direction.Down | Direction.South, // 0b_00000111
        Positive = Direction.East | Direction.Up | Direction.North,   // 0b_00111000

        All = 0b_11111111,
    }

    public enum FaceAxis : byte
    {
        WestEast,
        DownUp,
        SouthNorth,
    }

    static class Extensions
    {
        public static Direction NegativeDirection(this FaceAxis faceAxis)
        {
            return (Direction)(1 << (int)faceAxis);
        }

        public static Direction PositiveDirection(this FaceAxis faceAxis)
        {
            return (Direction)(1 << ((int)faceAxis + 3));
        }

        static Direction OppositeDirection(this Direction direction)
        {
            Direction n = direction & Direction.Negative;
            Direction p = direction & Direction.Positive;
            int n_opposite = (int)n << 3;
            int p_opposite = (int)p >> 3;
            return (Direction)(n_opposite | p_opposite);
        }

        public static Vector3Int Step(this Vector3Int coordinates, Direction direction, int steps = 1) 
        {
            return direction switch
            {
                Direction.None => coordinates,
                Direction.West => coordinates + Vector3Int.left * steps,
                Direction.East => coordinates + Vector3Int.right * steps,
                Direction.Down => coordinates + Vector3Int.down * steps,
                Direction.Up => coordinates + Vector3Int.up * steps,
                Direction.South => coordinates + Vector3Int.back * steps,
                Direction.North => coordinates + Vector3Int.forward * steps,
                _ => throw new ArgumentException(),
            };
        }

        // TODO - public static Direction TurnLeft(this Direction direction)

        // TODO - public static Direction TurnRight(this Direction direction)

        public static Vector3Int RandomStep(this Vector3Int coordinates, Direction allowedDirections = Direction.All)
        {
            int allowedCount = CountBits((uint)allowedDirections);
            if (allowedCount <= 0) return coordinates;

            int r = UnityEngine.Random.Range(0, allowedCount);
            Direction stepDirection = Direction.None;

            for (int i = 0; i < 6; i++)
            {
                Direction candidate = (Direction)(1 << i);
                if (allowedDirections.HasFlag(candidate))
                {
                    if (r == 0)
                    {
                        stepDirection = candidate;
                        break;
                    }
                    r--;
                }
            }

            return coordinates.Step(stepDirection);
        }

        public static bool IsInRange(this Vector3Int a, Vector3Int min, Vector3Int max)
        {
            if (min.x > max.x) (min.x, max.x) = (max.x, min.x);
            if (min.y > max.y) (min.y, max.y) = (max.y, min.y);
            if (min.z > max.z) (min.z, max.z) = (max.z, min.z);

            if (a.x < min.x || a.x >= max.x) return false;
            if (a.y < min.y || a.y >= max.y) return false;
            if (a.z < min.z || a.z >= max.z) return false;

            return true;
        }

        public static bool GetRandomItem<ItemType>(this IReadOnlyCollection<ItemType> items,
            out ItemType randomItem,
            out int itemIndex)
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

        public static Direction LateralDirectionTowards(this Vector3Int coordinates, Vector3Int targetCoordinates)
        {
            Vector3Int delta = targetCoordinates - coordinates;
            delta.y = 0; // ignore vertical differences
            Vector3Int absDelta = new Vector3Int(Mathf.Abs(delta.x), 0, Mathf.Abs(delta.z));
            int maxDelta = (absDelta.x > absDelta.z) ? absDelta.x : absDelta.z;
            if (maxDelta == 0) return Direction.None;
            Direction direction;
            if (delta.x <= -maxDelta) direction = Direction.West;
            else if (delta.x >= maxDelta) direction = Direction.East;
            else if (delta.z <= -maxDelta) direction = Direction.South;
            else direction = Direction.North;
            return direction;
        }

        public static Vector3Int LateralStepTowards(this Vector3Int coordinates, Vector3Int targetCoordinates)
        {
            Direction direction = coordinates.LateralDirectionTowards(targetCoordinates);
            return coordinates.Step(direction);
        }

        public static Direction DirectionTowards(this Vector3Int coordinates, Vector3Int targetCoordinates)
        {
            Vector3Int delta = targetCoordinates - coordinates;
            Vector3Int absDelta = new Vector3Int(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z));
            int maxDelta = (absDelta.x > absDelta.z) ? absDelta.x : absDelta.z;
            maxDelta = (absDelta.y > maxDelta) ? absDelta.y : maxDelta;
            if (maxDelta == 0) return Direction.None;
            Direction direction;

            if      (delta.x <= -maxDelta) direction = Direction.West;
            else if (delta.x >= +maxDelta) direction = Direction.East;

            else if (delta.y <= -maxDelta) direction = Direction.Down;
            else if (delta.y >= +maxDelta) direction = Direction.Up;

            else if (delta.z <= -maxDelta) direction = Direction.South;
            else direction = Direction.North;

            return direction;
        }

        public static Vector3Int RandomStepTowards(this Vector3Int coordinates, Vector3Int targetCoordinates, Direction allowedDirections = Direction.All)
        {
            Direction direction = coordinates.DirectionTowards(targetCoordinates);
            Direction opposite = direction.OppositeDirection();
            allowedDirections &= ~opposite;
            return coordinates.RandomStep(allowedDirections);
        }

        public static int CountBits(uint bits)
        {
            int count;
            for (count = 0; bits != 0; count++)
            {
                bits &= bits - 1; // clear the least significant bit set
            }
            return count;
        }
    }

    public class Caves : MonoBehaviour
    {
        [SerializeField] Vector3 cellSize = new(2f, 1.5f, 2f);
        [SerializeField] Vector3Int gridSize = new(64, 8, 64);
        [SerializeField] float wallThickness = 0.3f;
        [SerializeField, Tooltip("0 = randomize")] int seed;
        [SerializeField, Range(0f, 1f)] float randomRoomCenters = 0.5f;
        [SerializeField] bool windingPassages;
        int RoomCount => mazeWidth * mazeWidth;
        [SerializeField, Range(1, 10)] int mazeWidth = 4;
        [SerializeField] Material wallMaterial;
        [SerializeField] Material floorCeilingMaterial;

        // just for testing
        [SerializeField] bool createMaze;

        const float COROUTINE_TIME_SLICE = 0.05f; // seconds

        private bool[/*X*/,/*Y*/,/*Z*/] cells; // false = closed, true = open
        private readonly HashSet<GridWall> allWalls = new();
        private Vector3Int[] roomCenters; // "center" position of each room in the cave system

        public bool IsCellOpen(Vector3Int coordinates)
        {
            if (!coordinates.IsInRange(Vector3Int.zero, gridSize)) return false;
            return cells[coordinates.x, coordinates.y, coordinates.z];
        }

        private void SetWallState(Vector3Int coordinates, FaceAxis faceAxis, bool isPresent,
            ICollection<GridWall> wallsAdded = null,
            ICollection<GridWall> wallsRemoved = null)
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

        private GameObject CreateWallObject(GridWall wall, Transform wallParent)
        {
            var wallObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallObj.transform.parent = wallParent;
            wallObj.name = $"Wall {wall.coordinates} {wall.faceAxis}";
            wallObj.GetComponent<Renderer>().material = (wall.faceAxis == FaceAxis.DownUp) ? floorCeilingMaterial : wallMaterial;
            Vector3 halfStep = cellSize / 2f;
            Vector3 position = wall.coordinates;
            position.Scale(cellSize);
            Vector3 scale = cellSize;
            switch (wall.faceAxis)
            {
                case FaceAxis.WestEast:
                    position += new Vector3(0f, halfStep.y, halfStep.z);
                    scale.x *= wallThickness;
                    break;

                case FaceAxis.DownUp:
                    position += new Vector3(halfStep.x, 0f, halfStep.z);
                    scale.y *= wallThickness;
                    break;

                case FaceAxis.SouthNorth:
                    position += new Vector3(halfStep.x, halfStep.y, 0f);
                    scale.z *= wallThickness;
                    break;

                default:
                    throw new InvalidEnumArgumentException();
            }
            wallObj.transform.position = position;
            wallObj.transform.localScale = scale;
            return wallObj;
        }

        private void Awake()
        {
            cells = new bool[gridSize.x, gridSize.y, gridSize.z];            
            roomCenters = new Vector3Int[RoomCount];
            if (seed == 0)
            {
                seed = (int)DateTime.Now.Ticks;
            }
            UnityEngine.Random.InitState(seed);
        }

        private void Start()
        {
            if (createMaze)
            {
                StartCoroutine(CreateMaze());
            }
            else
            {
                StartCoroutine(CreateCaves());
            }

            // Because CreateCaves is a coroutine, we can do other stuff after CreateCaves has yielded.
            // CreateCaves will resume once per frame until it has completed or called "yield break".
        }

        /************************************************************************/

        private IEnumerator CreateMaze()
        {
            GridMaze maze = new GridMaze(gridSize);
            var justVerticalWalls = maze.Walls.Where(wall => wall.faceAxis != FaceAxis.DownUp);
            allWalls.UnionWith(justVerticalWalls);
            yield return CreateAllWallObjects();
        }

        private IEnumerator CreateCaves()
        {
            for (int roomNumber = 0; roomNumber < RoomCount; roomNumber++)
            {
                yield return ExcavateRoom(roomNumber);
            }

            // Create a maze that connects rooms with passages
            Vector3Int mazeSize = new Vector3Int(mazeWidth, 1, mazeWidth);
            GridMaze roomMaze = new GridMaze(mazeSize);
            roomMaze.Generate();

            // Visit each room and excavate a passage to its west and south
            // neighbor rooms, unless there is a maze wall between them.
            for (int currentRoomIndex = 0; currentRoomIndex < RoomCount; currentRoomIndex++)
            {
                int x = currentRoomIndex % mazeWidth;
                int z = currentRoomIndex / mazeWidth;
                Vector3Int currentRoomCoords = new Vector3Int(x, 0, z);

                GridWall westWall = new GridWall(currentRoomCoords, FaceAxis.WestEast);
                GridWall southWall = new GridWall(currentRoomCoords, FaceAxis.SouthNorth);

                if (!roomMaze.Walls.Contains(westWall))
                {
                    Vector3Int west = currentRoomCoords.Step(Direction.West);
                    int westRoomIndex = west.x + west.z * mazeWidth;
                    yield return ExcavatePassageBetweenRoomCenters(currentRoomIndex, westRoomIndex);
                }

                if (!roomMaze.Walls.Contains(southWall))
                {
                    Vector3Int south = currentRoomCoords.Step(Direction.South);
                    int southRoomIndex = south.x + south.z * mazeWidth;
                    yield return ExcavatePassageBetweenRoomCenters(currentRoomIndex, southRoomIndex);
                }
            }

            yield return CreateAllWallObjects();
        }

        private IEnumerator ExcavateRoom(int roomNumber)
        {
            GetRoomCenterAndSize(roomNumber, out Vector3Int center, out Vector3Int roomSize);
            roomCenters[roomNumber] = center;
            int roomCellCount = roomSize.x * roomSize.y * roomSize.z;
            yield return ExcavateVolume(center, roomCellCount); // TODO - limit excavation to min/max
            print($"Room {roomNumber} has {roomCellCount} cells placed at {center} with a max size of {roomSize}");
        }

        private IEnumerator CreateAllWallObjects()
        {
            float time = Time.realtimeSinceStartup;
            foreach (GridWall wall in allWalls)
            {
                CreateWallObject(wall, transform);

                // TIME SLICE
                // Periodically give control back to Unity's update loop,
                // so that the app remains interactive and avoid freezing.
                if (Time.realtimeSinceStartup - time > COROUTINE_TIME_SLICE)
                {
                    time = Time.realtimeSinceStartup;
                    yield return null;
                }
            }

            print($"Created {allWalls.Count} wall objects");
        }

        private void GetRoomCenterAndSize(int roomNumber, out Vector3Int center, out Vector3Int roomSize)
        {
            int gridWidth = Mathf.CeilToInt(Mathf.Sqrt(RoomCount));
            Vector3Int blockSize = gridSize / gridWidth;
            blockSize = Vector3Int.Max(blockSize, Vector3Int.one);
            Vector3Int halfBlockSize = blockSize / 2;
            roomSize = Vector3Int.Max(halfBlockSize, Vector3Int.one);
            center = halfBlockSize;
            // move the room's center to the its unique placement in the larger grid of rooms
            int row = roomNumber / gridWidth;
            int column = roomNumber % gridWidth;
            center.x += blockSize.x * column;
            center.y = 0;
            center.z += blockSize.z * row;

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
        }

        /// <summary>
        /// Returns a random coordinate between the minimum and maximum corners.
        /// </summary>
        /// <param name="min">inclusive</param>
        /// <param name="max">exclusive</param>
        /// <returns></returns>
        public static Vector3Int GetRandomCell(Vector3Int min, Vector3Int max)
        {
            Vector3Int coordindates = new Vector3Int(
                UnityEngine.Random.Range(min.x, max.x),
                UnityEngine.Random.Range(min.y, max.y),
                UnityEngine.Random.Range(min.z, max.z));
            return coordindates;
        }

        public IEnumerator ExcavateVolume(Vector3Int coordinates, int maxCellCount)
        {
            var wallsOfVolume = new HashSet<GridWall>();
            var wallsAdded = new List<GridWall>();
            var wallsRemoved = new List<GridWall>();

            if (!TryExcavateStandingSpace(coordinates, wallsAdded, wallsRemoved))
            {
                yield break; // cannot make a volume at these coordinates
            }
            UpdateSetOfWalls(wallsOfVolume, wallsAdded, wallsRemoved);

            // Excavate random walls until we hit a limit (or none remain)
            float time = Time.realtimeSinceStartup;
            for (int i = 0; i < maxCellCount; i++)
            {
                if (!wallsOfVolume.GetRandomItem(out GridWall wall, out _))
                {
                    break; // no more walls, so stop trying
                }
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

            // !CHALLENGE! remove or prevent "floating islands"
        }

        public bool TryExcavateWall(GridWall wall,
            ICollection<GridWall> wallsAdded = null,
            ICollection<GridWall> wallsRemoved = null)
        {
            bool okay = TryExcavateStandingSpace(wall.coordinates, wallsAdded, wallsRemoved);
            Vector3Int otherSide = wall.coordinates.Step(wall.faceAxis.NegativeDirection());
            okay |= TryExcavateStandingSpace(otherSide, wallsAdded, wallsRemoved);
            return okay;
        }

        public bool TryExcavateStandingSpace(Vector3Int coordinates,
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

        public bool TryExcavateCell(Vector3Int coordinates,
            ICollection<GridWall> wallsAdded = null,
            ICollection<GridWall> wallsRemoved = null)
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

        private void UpdateSetOfWalls(HashSet<GridWall> wallSet,
            ICollection<GridWall> wallsAdded,
            ICollection<GridWall> wallsRemoved)
        {
            wallSet.UnionWith(wallsAdded);
            wallSet.ExceptWith(wallsRemoved);
            wallsAdded.Clear();
            wallsRemoved.Clear();
        }

        private IEnumerator ExcavatePassageBetweenRoomCenters(int roomA, int roomB)
        {
            Vector3Int a = roomCenters[roomA];
            Vector3Int b = roomCenters[roomB];
            // TODO find floors at a and b
            yield return ExcavatePassage(a, b);
        }

        private IEnumerator ExcavatePassage(Vector3Int fromCoordinates, Vector3Int toCoordinates)
        {
            TryExcavateStandingSpace(fromCoordinates);
            Vector3Int c = fromCoordinates;
            float time = Time.realtimeSinceStartup;
            int failSafe = (gridSize.x + gridSize.y + gridSize.z) * 10;
            int passageLength = 0;
            while ((c.x != toCoordinates.x || c.z != toCoordinates.z) && passageLength < failSafe)
            {
                if (windingPassages)
                {
                    Direction allowedDirections = Direction.All;

                    // disallow "flying"
                    if (IsCellOpen(c.Step(Direction.Down))) allowedDirections &= ~Direction.Up;

                    // disallow digging deep holes
                    if (IsCellOpen(c.Step(Direction.Up, steps: 2))) allowedDirections &= ~Direction.Down;

                    c = c.RandomStepTowards(toCoordinates, allowedDirections);
                }
                else
                {
                    c = c.LateralStepTowards(toCoordinates);
                }

                // don't step out of bounds
                c.Clamp(Vector3Int.zero, gridSize - Vector3Int.one * 2);

                TryExcavateStandingSpace(c);
                passageLength++;

                if (Time.realtimeSinceStartup - time > COROUTINE_TIME_SLICE)
                {
                    time = Time.realtimeSinceStartup;
                    yield return null;
                }
            }
        }
    }
}
