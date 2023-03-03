using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lessons
{
    public readonly struct GridWall
    {
        public readonly Vector3Int coordinates; // walls at (x,y,z,*) are on the West, Down, and South sides of the cell at (x,y,z)
        public readonly FaceAxis faceAxis; // axis is perpendicular to the face

        public GridWall(Vector3Int coordinates, FaceAxis faceAxis)
        {
            this.coordinates = coordinates;
            this.faceAxis = faceAxis;
        }

        public Vector3Int PositiveSide => coordinates;
        public Vector3Int NegativeSide => coordinates.Step(faceAxis.NegativeDirection());
    }
}
