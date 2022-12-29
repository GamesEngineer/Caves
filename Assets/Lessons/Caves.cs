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
            return (Direction)(1 << (int)faceAxis);
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

    public class Caves : MonoBehaviour
    {
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