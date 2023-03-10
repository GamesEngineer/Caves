using GameU;
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

        All     = 0b_11111111,
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

        // TODO - public static Direction OppositeDirection(this Direction direction)   

        public static Vector3Int Step(this Vector3Int coordinates, Direction direction) 
        {
            return direction switch
            {
                Direction.None => coordinates,
                Direction.West => coordinates + Vector3Int.left,
                Direction.East => coordinates + Vector3Int.right,
                Direction.Down => coordinates + Vector3Int.down,
                Direction.Up => coordinates + Vector3Int.up,
                Direction.South => coordinates + Vector3Int.back,
                Direction.North => coordinates + Vector3Int.forward,
                _ => throw new ArgumentException(),
            };
        }

        // TODO - public static Direction TurnLeft(this Direction direction)

        // TODO - public static Direction TurnRight(this Direction direction)

        // TODO - public static Vector3Int RandomStep(this Vector3Int coordinates, Direction allowedDirections = Direction.All)

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

        // TODO - public static Direction DirectionTowards(this Vector3Int coordinates, Vector3Int targetCoordinates)

        // TODO - public static Vector3Int RandomStepTowards(this Vector3Int coordinates, Vector3Int targetCoordinates, Direction allowedDirections = Direction.All)

        // TODO - public static int CountBits(uint bits)
    }

    public class Caves : MonoBehaviour
    {
        [SerializeField] Vector3 cellSize = new(2f, 1.5f, 2f);
        [SerializeField] Vector3Int gridSize = new(64, 8, 64);
        [SerializeField] float wallThickness = 0.3f;
        [SerializeField, Tooltip("0 = randomize")] int seed = 0;
        [SerializeField, Range(0f, 1f)] float randomRoomCenters = 0.5f;
        [SerializeField] bool windingPassages = false;
        [SerializeField] int roomCount = 1;
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
            roomCenters = new Vector3Int[roomCount];
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
            GridMaze maze = new(gridSize);
            var justVerticalWalls = maze.Walls.Where(wall => wall.faceAxis != FaceAxis.DownUp);
            allWalls.UnionWith(justVerticalWalls);
            yield return CreateAllWallObjects();
        }

        private IEnumerator CreateCaves()
        {
            for (int roomNumber = 0; roomNumber < roomCount; roomNumber++)
            {
                yield return ExcavateRoom(roomNumber);
            }

            // TODO - excavate passages between rooms
            yield return ExcavatePassageBetweenRoomCenters(0, 1);

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
            int gridWidth = Mathf.CeilToInt(Mathf.Sqrt(roomCount));
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
                c = c.LateralStepTowards(toCoordinates);
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
