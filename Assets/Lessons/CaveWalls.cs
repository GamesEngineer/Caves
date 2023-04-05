using System.ComponentModel;
using System.Collections.Generic;
using UnityEngine;

namespace Lessons
{
    [RequireComponent(typeof(Caves))]
    public class CaveWalls : MonoBehaviour
    {
        [SerializeField] Vector3 cellSize = new Vector3(2f, 1.5f, 2f);
        [SerializeField] Material wallMaterial;
        [SerializeField] float wallThickness = 0.3f;

        private Caves caves;
        private Mesh wallMesh;
        private readonly List<BatchOfMatrices> wallMatrices = new();
        BatchOfMatrices currentBatchOfMatrices;

        void Awake()
        {
            caves = GetComponent<Caves>();
            caves.OnCreated += Caves_OnCreated;
            wallMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        }

        private void Caves_OnCreated()
        {
            foreach (GridWall wall in caves.Walls)
            {
                AddWall(wall);
            }
            print($"Created {caves.Walls.Count} walls and added {wallMatrices.Count} batches");
        }

        void Update()
        {
            foreach (BatchOfMatrices batch in wallMatrices)
            {
                Graphics.DrawMeshInstanced(wallMesh, gameObject.layer, wallMaterial, batch.Matrices, batch.Count);
            }
        }

        private void AddWall(GridWall wall)
        {
            if (currentBatchOfMatrices is null || currentBatchOfMatrices.IsFull)
            {
                currentBatchOfMatrices = new BatchOfMatrices();
                wallMatrices.Add(currentBatchOfMatrices);
            }

            Vector3 scale = cellSize;
            Vector3 position = wall.coordinates;
            position.Scale(cellSize); // converts from WALL coordinates to WORLD coordinates
            // Due to how we encode wall coordinates, 'position' is located at the corner
            // (west, down, south) of the grid cell that "owns" this wall. We need to
            // translate the center of the wall's mesh to the center of the appropriate face
            // of its grid cell.
            // Also, a cube mesh doesn't look like a thin wall. So, we need to flatten it (scale)
            // along the wall's face-axis.
            switch (wall.faceAxis)
            {
                case FaceAxis.WestEast:
                    position += new Vector3(0f, cellSize.y, cellSize.z) / 2f;
                    scale.x *= wallThickness;
                    break;
                case FaceAxis.DownUp:
                    position += new Vector3(cellSize.x, 0f, cellSize.z) / 2f;
                    scale.y *= wallThickness;
                    break;
                case FaceAxis.SouthNorth:
                    position += new Vector3(cellSize.x, cellSize.y, 0f) / 2f;
                    scale.z *= wallThickness;
                    break;
                default:
                    throw new InvalidEnumArgumentException();
            }
            currentBatchOfMatrices.Add(position, scale);
        }

        private class BatchOfMatrices
        {
            public Matrix4x4[] Matrices { get; private set; } = new Matrix4x4[MAX_COUNT];
            public const int MAX_COUNT = 1023; // Unity's DrawMeshInstanced has a limit of 1023 instances per call
            public int Count { get; private set; }
            public bool IsFull => Count >= MAX_COUNT;
            public void Add(Vector3 position, Vector3 scale)
            {
                Matrix4x4 matrix = new();
                matrix.SetTRS(position, Quaternion.identity, scale);
                Matrices[Count++] = matrix;
            }
        }
    }
}
