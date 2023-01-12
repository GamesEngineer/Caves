using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace GameU
{
    public enum FaceAxis : byte
    {
        WestEast,
        DownUp,
        SouthNorth,
    }

    [Flags]
    public enum Direction : byte
    {
        None = 0,

        West = 0b_000001,
        Down = 0b_000010,
        South = 0b_000100,

        East = 0b_001000,
        Up = 0b_010000,
        North = 0b_100000,

        All = 0b_111111,
    }

    internal static class ExtensionsMethods
    {
        public static Vector3Int Step(this Vector3Int coordinates, Direction direction) => direction switch
        {
            Direction.None => coordinates,
            Direction.West => coordinates + Vector3Int.left,
            Direction.East => coordinates + Vector3Int.right,
            Direction.Down => coordinates + Vector3Int.down,
            Direction.Up => coordinates + Vector3Int.up,
            Direction.South => coordinates + Vector3Int.back,
            Direction.North => coordinates + Vector3Int.forward,
            _ => throw new InvalidOperationException(),
        };

        public static Vector3Int RandomStepTowards(this Vector3Int coordinates, Vector3Int toCoordinates)
        {
            Direction direction = Direction.None;
            Vector3Int delta = toCoordinates - coordinates;
            if      (delta.x < 0) direction = Direction.West;
            else if (delta.x > 0) direction = Direction.East;
            else if (delta.y < 0) direction = Direction.Down;
            else if (delta.y > 0) direction = Direction.Up;
            else if (delta.z < 0) direction = Direction.South;
            else if (delta.z > 0) direction = Direction.North;

            int r = UnityEngine.Random.Range(0, 6);
            Direction randomDirection = (Direction)(1 << r);
            if (randomDirection != direction.OppositeDirection()
#if true
                && randomDirection != Direction.Up
                && randomDirection != Direction.Down
#endif
                )
            {
                direction = randomDirection;
            }

            return coordinates.Step(direction);
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

        public static FaceAxis ToFaceAxis(this Direction direction)
        {
            return direction switch
            {
                Direction.West => FaceAxis.WestEast,
                Direction.East => FaceAxis.WestEast,
                Direction.Down => FaceAxis.DownUp,
                Direction.Up => FaceAxis.DownUp,
                Direction.South => FaceAxis.SouthNorth,
                Direction.North => FaceAxis.SouthNorth,
                _ => throw new InvalidOperationException(),
            };
        }

        public static Direction NegativeDirection(this FaceAxis faceAxis) => (Direction)(1 << (int)faceAxis);

        public static Direction PositiveDirection(this FaceAxis faceAxis) => (Direction)(1 << ((int)faceAxis + 3));

        public static Direction OppositeDirection(this Direction direction) => direction switch
        {
            Direction.None => Direction.None,
            Direction.West => Direction.East,
            Direction.East => Direction.West,
            Direction.Down => Direction.Up,
            Direction.Up => Direction.Down,
            Direction.South => Direction.North,
            Direction.North => Direction.South,
            _ => throw new InvalidOperationException(),
        };

        public static Direction TurnLeft(this Direction direction) => direction switch
        {
            Direction.None => Direction.None,
            Direction.West => Direction.South,
            Direction.East => Direction.North,
            Direction.Down => Direction.Down,
            Direction.Up => Direction.Up,
            Direction.South => Direction.East,
            Direction.North => Direction.West,
            _ => throw new InvalidOperationException(),
        };
        
        public static Direction TurnRight(this Direction direction) => direction switch
        {
            Direction.None => Direction.None,
            Direction.West => Direction.North,
            Direction.East => Direction.South,
            Direction.Down => Direction.Down,
            Direction.Up => Direction.Up,
            Direction.South => Direction.West,
            Direction.North => Direction.East,
            _ => throw new InvalidOperationException(),
        };

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

    public readonly struct GridWall
    {
        public readonly Vector3Int coordinates; // walls at (x,y,z,*) are on the West, Down, and South sides of the cell at (x,y,z)
        public readonly FaceAxis faceAxis; // axis is perpendicular to the face

        public GridWall(Vector3Int coordinates, FaceAxis faceAxis)
        {
            this.coordinates = coordinates;
            this.faceAxis = faceAxis;
        }
    }

    public class CaveSystem : MonoBehaviour
    {
        [SerializeField] Vector3 cellSize = new(2f, 1.5f, 2f);
        [SerializeField] Vector3Int gridSize = new(64, 8, 64);
        [SerializeField] int roomCount = 4;
        [SerializeField] bool randomRoomCenters = true;
        [SerializeField] float wallThickness = 0.3f;
        [SerializeField] Material wallMaterial;
        [SerializeField] Material floorCeilingMaterial;
        [SerializeField] Light lightPrefab;
        [SerializeField, Tooltip("0 = randomize")] int seed = 0;

        public event Action OnCreated;
        
        private bool[/*X*/,/*Y*/,/*Z*/] cells; // false = closed, true = open
        private Vector3Int[] roomCenters; // "center" position of each room
        private readonly HashSet<GridWall> allWalls = new();

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

        private void SetWallState(Vector3Int coordinates, FaceAxis faceAxis, bool isPresent, ICollection<GridWall> wallsAdded, ICollection<GridWall> wallsRemoved)
        {
            if (!coordinates.IsInRange(Vector3Int.zero, gridSize + Vector3Int.one))
            {
                throw new ArgumentOutOfRangeException();
            }

            var wall = new GridWall(coordinates, faceAxis);

            if (isPresent)
            {
                allWalls.Add(wall);
                if (wallsAdded is not null) wallsAdded.Add(wall);
            }
            else
            {
                allWalls.Remove(wall);
                if (wallsRemoved is not null) wallsRemoved.Add(wall);
            }
        }

        public bool TryExcavateCell(Vector3Int coordinates, ICollection<GridWall> wallsAdded = null, ICollection<GridWall> wallsRemoved = null)
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

        public bool TryExcavateStandingSpace(Vector3Int coordinates, ICollection<GridWall> wallsAdded = null, ICollection<GridWall> wallsRemoved = null)
        {
            bool okay = TryExcavateCell(coordinates, wallsAdded, wallsRemoved);
            if (!okay) return false;
            // Open the cell above. This assumes that character height is 2x cell height.
            coordinates = coordinates.Step(Direction.Up);
            TryExcavateCell(coordinates, wallsAdded, wallsRemoved); // it's okay for this to fail
            return true;
        }

        public bool TryExcavateWall(GridWall wall, ICollection<GridWall> wallsAdded = null, ICollection<GridWall> wallsRemoved = null)
        {
            bool okay = TryExcavateStandingSpace(wall.coordinates, wallsAdded, wallsRemoved);
            Vector3Int otherSide = wall.coordinates.Step(wall.faceAxis.NegativeDirection());
            okay |= TryExcavateStandingSpace(otherSide, wallsAdded, wallsRemoved);
            return okay;
        }

        public bool TryGetFloorPosition(ref Vector3Int coordinates, out Vector3 floorPosition)
        {
            floorPosition = Vector3.zero;
            if (!IsCellOpen(coordinates)) return false;

            // Find the floor cell
            Vector3Int c = coordinates;
            while (c.y > 0)
            {
                c.y--;
                if (!IsCellOpen(c)) break;
                coordinates = c;
            }

            floorPosition = GetCellFacePosition(coordinates, Direction.Down) + Vector3.up * wallThickness;
            return true;
        }

        private void UpdateSetOfWalls(HashSet<GridWall> wallSet, ICollection<GridWall> wallsAdded, ICollection<GridWall> wallsRemoved)
        {
            wallSet.UnionWith(wallsAdded);
            wallSet.ExceptWith(wallsRemoved);
            wallsAdded.Clear();
            wallsRemoved.Clear();
        }

        public IEnumerator ExcavateVolume(Vector3Int coordinates, int maxCellCount)
        {
            HashSet<GridWall> wallsOfVolume = new();
            List<GridWall> wallsAdded = new();
            List<GridWall> wallsRemoved = new();

            if (!TryExcavateStandingSpace(coordinates, wallsAdded, wallsRemoved))
            {
                yield break; // cannot make a volume at these coordinates
            }
            UpdateSetOfWalls(wallsOfVolume, wallsAdded, wallsRemoved);

            int frame = Time.frameCount;
            for (int i = 0; i < maxCellCount; i++)
            {
                if (!wallsOfVolume.GetRandomItem(out GridWall wall, out _)) break;
                TryExcavateWall(wall, wallsAdded, wallsRemoved);
                UpdateSetOfWalls(wallsOfVolume, wallsAdded, wallsRemoved);

                // Time slice
                if (Time.frameCount != frame)
                {
                    yield return null;
                    frame = Time.frameCount;
                }
            }
        }

        private IEnumerator ExcavateRoom(int roomNumber)
        {
            int divisor = Mathf.CeilToInt(Mathf.Sqrt((float)(roomCenters.Length)));
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

            if (randomRoomCenters)
            {
                Vector3Int minInclusive = center - roomSize;
                Vector3Int maxExclusive = center + roomSize + Vector3Int.one;
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
            int frame = Time.frameCount;
            int failSafe = gridSize.x + gridSize.y + gridSize.z;
            while (c != toCoordinates && failSafe-- >= 0)
            {
                c = c.RandomStepTowards(toCoordinates);
                c.Clamp(Vector3Int.zero, gridSize - Vector3Int.one);
                TryExcavateStandingSpace(c);
                // Time slice
                if (Time.frameCount != frame)
                {
                    yield return null;
                    frame = Time.frameCount;
                }
            }
        }

        private GameObject CreateWallObject(GridWall wall, Transform wallParent)
        {
            var wallObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallObj.transform.parent = wallParent;
            wallObj.name = $"Wall {wall.coordinates} {wall.faceAxis}";
            wallObj.GetComponent<Renderer>().material = (wall.faceAxis == FaceAxis.DownUp) ? floorCeilingMaterial : wallMaterial;
            SetWallTransform(wall, wallObj.transform, wallThickness);
            return wallObj;
        }

        private void SetWallTransform(GridWall wall, Transform wallTransform, float wallThickness)
        {
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
            wallTransform.transform.position = position;
            wallTransform.transform.localScale = scale;
        }

        private bool RemoveWallObject(GridWall wall, Transform wallParent)
        {
            for (int i=0; i< wallParent.childCount; i++)
            {
                Transform child = wallParent.GetChild(i);
                Vector3 position = GetWallPosition(wall);
                if (Vector3.SqrMagnitude(child.position - position) < 0.1f)
                {
                    Destroy(child.gameObject);
                    return true;
                }
            }
            return false;
        }

        private Vector3 GetCellPosition(Vector3Int coordinates)
        {
            Vector3 offset = new((float)coordinates.x + 0.5f, (float)coordinates.y + 0.5f, (float)coordinates.z + 0.5f);
            offset.Scale(cellSize);
            return transform.position + offset;
        }

        private Vector3 GetCellFacePosition(Vector3Int coordinates, Direction direction)
        {
            Vector3 position = GetCellPosition(coordinates);
            Vector3 halfStep = cellSize / 2f;
            if (direction.HasFlag(Direction.North)) position.z += halfStep.z;
            if (direction.HasFlag(Direction.South)) position.z -= halfStep.z;
            if (direction.HasFlag(Direction.Up)) position.y += halfStep.y;
            if (direction.HasFlag(Direction.Down)) position.y -= halfStep.y;
            if (direction.HasFlag(Direction.East)) position.x += halfStep.x;
            if (direction.HasFlag(Direction.West)) position.x -= halfStep.x;
            return position;
        }
        
        private Vector3 GetWallPosition(GridWall wall)
        {
            Vector3 halfStep = cellSize / 2f;
            Vector3 position = wall.coordinates;
            position.Scale(cellSize);
            switch (wall.faceAxis)
            {
                case FaceAxis.WestEast:
                    position += new Vector3(0f, halfStep.y, halfStep.z);
                    break;

                case FaceAxis.DownUp:
                    position += new Vector3(halfStep.x, 0f, halfStep.z);
                    break;

                case FaceAxis.SouthNorth:
                    position += new Vector3(halfStep.x, halfStep.y, 0f);
                    break;

                default:
                    throw new InvalidEnumArgumentException();
            }
            return position;
        }

        private IEnumerator Excavate()
        {
            for (int roomNumber = 0; roomNumber < roomCount; roomNumber++)
            {
                yield return ExcavateRoom(roomNumber);
            }

            // excavate passages between rooms
            // TODO - use a modified maze algorithm
            for (int toRoom = 1; toRoom < roomCount; toRoom++)
            {
                int fromRoom = toRoom - 1;
                yield return ExcavatePassage(roomCenters[fromRoom], roomCenters[toRoom]);
            }

            foreach (GridWall wall in allWalls)
            {
                CreateWallObject(wall, transform);
            }

            Illuminate();

            OnCreated?.Invoke();
        }

        private void Illuminate()
        {
            foreach (Vector3Int roomCoordinates in roomCenters)
            {
                Vector3Int coordinates = roomCoordinates.Step(Direction.Up);
                Vector3 position = GetCellFacePosition(coordinates, Direction.None);
                Light light = Instantiate(lightPrefab, position, Quaternion.identity);
                light.transform.parent = transform;
                light.name = $"Light {coordinates}";
            }
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
            StartCoroutine(Excavate());
        }
    }
}
