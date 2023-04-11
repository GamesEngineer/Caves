using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace GameU
{
    [RequireComponent(typeof(CaveSystem))]
    public class CaveWalls : MonoBehaviour
    {
        [SerializeField] Vector3 cellSize = new(2f, 1.5f, 2f);
        [SerializeField] float wallThickness = 0.3f;
        [SerializeField] Material wallMaterial;
        [SerializeField] Material floorMaterial;

        CaveSystem caves;
        Mesh wallMesh;
        readonly List<BatchOfMatrices> wallMatrices = new();
        readonly List<BatchOfMatrices> floorMatrices = new();
        BatchOfMatrices currentBatchOfWallMatrices;
        BatchOfMatrices currentBatchOfFloorMatrices;

        private void Awake()
        {
            caves = GetComponent<CaveSystem>();
            wallMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        }

        private void Start()
        {
            caves.OnCreated += Caves_OnCreated;
        }

        private void Update()
        {
            // NOTE: Use deferred rendering in order to get correct lighting.
            // Forward rendering cannot correctly illuminate batches that overlap too many lights (more than 8).
            foreach (BatchOfMatrices batch in wallMatrices)
            {
                Graphics.DrawMeshInstanced(wallMesh, gameObject.layer, wallMaterial, batch.Matrices, batch.Count);
            }

            foreach (BatchOfMatrices batch in floorMatrices)
            {
                Graphics.DrawMeshInstanced(wallMesh, gameObject.layer, floorMaterial, batch.Matrices, batch.Count);
            }
        }

        private void Caves_OnCreated()
        {
            foreach (GridWall wall in caves.Walls)
            {
                AddWall(wall);
            }
        }

        private class BatchOfMatrices
        {
            public Matrix4x4[] Matrices { get; private set; } = new Matrix4x4[MAX_COUNT];
            public int Count { get; private set; }
            public const int MAX_COUNT = 1023; // Unity's DrawMeshInstanced has a limit of 1023 instances per call
            public bool IsFull => (Count >= MAX_COUNT);

            public void Add(Vector3 position, Vector3 scale)
            {
                Matrix4x4 matrix = new();
                matrix.SetTRS(position, Quaternion.identity, scale);
                Matrices[Count++] = matrix;
            }
        }

        private void AddWall(GridWall wall)
        {
            if (currentBatchOfWallMatrices is null || currentBatchOfWallMatrices.IsFull)
            {
                currentBatchOfWallMatrices = new BatchOfMatrices();
                wallMatrices.Add(currentBatchOfWallMatrices);
            }

            if (currentBatchOfFloorMatrices is null || currentBatchOfFloorMatrices.IsFull)
            {
                currentBatchOfFloorMatrices = new BatchOfMatrices();
                floorMatrices.Add(currentBatchOfFloorMatrices);
            }

            Vector3 scale = cellSize;
            Vector3 position = wall.coordinates;
            position.Scale(cellSize); // convert from WALL coordinates to WORLD coordinates
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
                    currentBatchOfWallMatrices.Add(position, scale);
                    break;
                case FaceAxis.DownUp:
                    position += new Vector3(cellSize.x, 0f, cellSize.z) / 2f;
                    scale.y *= wallThickness;
                    currentBatchOfFloorMatrices.Add(position, scale);
                    break;
                case FaceAxis.SouthNorth:
                    position += new Vector3(cellSize.x, cellSize.y, 0f) / 2f;
                    scale.z *= wallThickness;
                    currentBatchOfWallMatrices.Add(position, scale);
                    break;
                default:
                    throw new InvalidEnumArgumentException();
            }            
        }

        /***********************************************************/

        public bool TryGetFloorPosition(ref Vector3Int coordinates, out Vector3 floorPosition)
        {
            floorPosition = Vector3.zero;
            if (!caves.IsCellOpen(coordinates)) return false;
            coordinates = caves.FindFloor(coordinates);
            floorPosition = GetCellFacePosition(coordinates, Direction.Down) + Vector3.up * wallThickness;
            return true;
        }

        public Vector3 GetCellPosition(Vector3Int coordinates)
        {
            Vector3 offset = new((float)coordinates.x + 0.5f, (float)coordinates.y + 0.5f, (float)coordinates.z + 0.5f);
            offset.Scale(cellSize);
            return transform.position + offset;
        }

        public Vector3 GetCellFacePosition(Vector3Int coordinates, Direction direction)
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
    }
}
