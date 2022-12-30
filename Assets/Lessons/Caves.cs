using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lesson
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
            return (Direction)(1 << (int)faceAxis); // TODO - 12/29/2022 Q: "Is this a bug?" A: No, it is correct.
        }

        public static Direction PositiveDirection(this FaceAxis faceAxis)
        {
            return (Direction)(1 << ((int)faceAxis + 3));
        }

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

    public class Caves : MonoBehaviour
    {
        [SerializeField] Vector3Int gridSize = new Vector3Int(64, 8, 64);

        private bool[/*X*/,/*Y*/,/*Z*/] cells; // false = closed, true = open
        private readonly HashSet<GridWall> allWalls = new HashSet<GridWall>();

        public bool IsCellOpen(Vector3Int coordinates)
        {
            if (!coordinates.IsInRange(Vector3Int.zero, gridSize)) return false;
            return cells[coordinates.x, coordinates.y, coordinates.z];
        }

        private void SetWallState(Vector3Int coordinates, FaceAxis faceAxis, bool isPresent)
        {
            if (!coordinates.IsInRange(Vector3Int.zero, gridSize + Vector3Int.one))
            {
                throw new ArgumentOutOfRangeException();
            }

            var wall = new GridWall(coordinates, faceAxis);

            if (isPresent)
            {
                allWalls.Add(wall);
            }
            else
            {
                allWalls.Remove(wall);
            }

            // TODO - handle wallsAdded and wallsRemoved
        }

        private void Awake()
        {
            
        }

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}