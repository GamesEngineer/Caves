using System;
using UnityEngine;

namespace Lessons
{
    [Flags]
    public enum Direction : byte
    {
        None = 0,
        
        West    = 0b_000001,
        Down    = 0b_000010,
        South   = 0b_000100,

        East    = 0b_001000,
        Up      = 0b_010000,
        North   = 0b_100000,

        WestEast = Direction.West | Direction.East,
        DownUp = Direction.Down | Direction.Up,
        SouthNorth = Direction.South | Direction.North,

        Negative = Direction.West | Direction.Down | Direction.South,
        Positive = Direction.East | Direction.Up | Direction.North,

        Lateral = Direction.West | Direction.East | Direction.South | Direction.North,
        Vertical = Direction.Down | Direction.Up,

        All = 0b_111111,
    }

    public enum FaceAxis : byte
    {
        WestEast = Direction.WestEast,
        DownUp = Direction.DownUp,
        SouthNorth = Direction.SouthNorth,
    }

    static class ExtensionMethods
    {
        public static Vector3Int Step(this Vector3Int coordinates, Direction direction, int steps = 1)
        {
            if (direction.HasFlag(Direction.West))  coordinates += Vector3Int.left * steps;
            if (direction.HasFlag(Direction.East))  coordinates += Vector3Int.right * steps;
            if (direction.HasFlag(Direction.Down))  coordinates += Vector3Int.down * steps;
            if (direction.HasFlag(Direction.Up))    coordinates += Vector3Int.up * steps;
            if (direction.HasFlag(Direction.South)) coordinates += Vector3Int.back * steps;
            if (direction.HasFlag(Direction.North)) coordinates += Vector3Int.forward * steps;
            return coordinates;
        }

        public static Direction NegativeDirection(this FaceAxis faceAxis) => (Direction)faceAxis & Direction.Negative;
        public static Direction PositiveDirection(this FaceAxis faceAxis) => (Direction)faceAxis & Direction.Positive;

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
}
