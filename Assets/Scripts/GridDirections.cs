using System;
using UnityEngine;

namespace GameU
{
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

        Lateral = Direction.West | Direction.East | Direction.South | Direction.North,
        Vertical = Direction.Down | Direction.Up,

        WestEast = Direction.West | Direction.East,
        DownUp = Direction.Down | Direction.Up,
        SouthNorth = Direction.South | Direction.North,

        Negative = Direction.West | Direction.Down | Direction.South,
        Positive = Direction.East | Direction.Up | Direction.North,

        All = 0b_111111,
    }

    public enum FaceAxis : byte
    {
        WestEast = Direction.WestEast,
        DownUp = Direction.DownUp,
        SouthNorth = Direction.SouthNorth,
    }

    static class ExtensionsMethods
    {
        public static Vector3Int Step(this Vector3Int coordinates, Direction direction, int steps = 1)
        {
            if (direction.HasFlag(Direction.West)) coordinates += Vector3Int.left * steps;
            if (direction.HasFlag(Direction.East)) coordinates += Vector3Int.right * steps;
            if (direction.HasFlag(Direction.Down)) coordinates += Vector3Int.down * steps;
            if (direction.HasFlag(Direction.Up)) coordinates += Vector3Int.up * steps;
            if (direction.HasFlag(Direction.South)) coordinates += Vector3Int.back * steps;
            if (direction.HasFlag(Direction.North)) coordinates += Vector3Int.forward * steps;
            return coordinates;
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

        public static Vector3Int RandomOrthogonalStep(this Vector3Int coordinates, Direction allowedDirections = Direction.All)
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

        public static Direction GetDirection(this Vector3Int fromCoordinates, Vector3Int toCoordinates)
        {
            Direction direction;
            Vector3Int delta = toCoordinates - fromCoordinates;
            Vector3Int absDelta = new(Math.Abs(delta.x), Math.Abs(delta.y), Math.Abs(delta.z));

            if (absDelta.x == 0 && absDelta.y == 0 && absDelta.z == 0)
            {
                direction = Direction.None;
            }
            else if (absDelta.x >= absDelta.y && absDelta.x >= absDelta.z)
            {
                direction = delta.x > 0 ? Direction.East : Direction.West;
            }
            else if (absDelta.y >= absDelta.x && absDelta.y >= absDelta.z)
            {
                direction = delta.y > 0 ? Direction.Up : Direction.Down;
            }
            else // (absDelta.z >= absDelta.x && absDelta.z >= absDelta.y)
            {
                direction = delta.z > 0 ? Direction.North : Direction.South;
            }

            return direction;
        }

        /// <param name="fromCoordinates"></param>
        /// <param name="toCoordinates"></param>
        /// <returns>Lateral direction that makes the most progress towards the goal</returns>
        public static Direction GetLateralDirection(this Vector3Int fromCoordinates, Vector3Int toCoordinates)
        {
            Direction direction;
            int xDistance = toCoordinates.x - fromCoordinates.x;
            int zDistance = toCoordinates.z - fromCoordinates.z;
            if (xDistance == 0 && zDistance == 0)
            {
                direction = Direction.None;
            }
            else if (Math.Abs(xDistance) >= Math.Abs(zDistance))
            {
                direction = xDistance > 0 ? Direction.East : Direction.West;
            }
            else
            {
                direction = zDistance > 0 ? Direction.North : Direction.South;
            }
            return direction;
        }

        public static Vector3Int LateralStepTowards(this Vector3Int fromCoordinates, Vector3Int toCoordinates)
        {
            Direction direction = GetLateralDirection(fromCoordinates, toCoordinates);
            return fromCoordinates.Step(direction);
        }

        public static Vector3Int RandomStepTowards(this Vector3Int coordinates, Vector3Int toCoordinates, Direction allowedDirections = Direction.All)
        {
            Direction direction = GetDirection(coordinates, toCoordinates);
            Direction opposite = direction.OppositeDirection();
            allowedDirections &= ~opposite;
            return RandomOrthogonalStep(coordinates, allowedDirections);
        }

        public static Vector3Int RandomLateralStepTowards(this Vector3Int fromCoordinates, Vector3Int toCoordinates, Direction allowedDirections = Direction.Lateral)
        {
            Direction direction = GetLateralDirection(fromCoordinates, toCoordinates);
            Direction opposite = direction.OppositeDirection();
            allowedDirections &= ~opposite;
            return RandomOrthogonalStep(fromCoordinates, allowedDirections);
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

        public static FaceAxis ToFaceAxis(this Direction direction) => direction switch
        {
            Direction.West => FaceAxis.WestEast,
            Direction.East => FaceAxis.WestEast,
            Direction.Down => FaceAxis.DownUp,
            Direction.Up => FaceAxis.DownUp,
            Direction.South => FaceAxis.SouthNorth,
            Direction.North => FaceAxis.SouthNorth,
            _ => throw new InvalidOperationException(),
        };

        public static Direction NegativeDirection(this FaceAxis faceAxis) => (Direction)faceAxis & Direction.Negative;

        public static Direction PositiveDirection(this FaceAxis faceAxis) => (Direction)faceAxis & Direction.Positive;

        public static Direction OppositeDirection(this Direction direction)
        {
            Direction n = direction & Direction.Negative;
            Direction p = direction & Direction.Positive;
            return (Direction)(((int)n << 3) | ((int)p >> 3));
        }

        public static Direction TurnLeft(this Direction direction) => direction switch
        {
            Direction.None => Direction.None,
            Direction.West => Direction.South,
            Direction.East => Direction.North,
            //Direction.Down => Direction.Down,
            //Direction.Up => Direction.Up,
            Direction.South => Direction.East,
            Direction.North => Direction.West,
            _ => throw new InvalidOperationException(),
        };

        public static Direction TurnRight(this Direction direction) => direction switch
        {
            Direction.None => Direction.None,
            Direction.West => Direction.North,
            Direction.East => Direction.South,
            //Direction.Down => Direction.Down,
            //Direction.Up => Direction.Up,
            Direction.South => Direction.West,
            Direction.North => Direction.East,
            _ => throw new InvalidOperationException(),
        };
    }
}
