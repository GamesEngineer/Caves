using GameU;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lessons
{
    [RequireComponent(typeof(Caves))]
    public class CaveWalls : MonoBehaviour
    {
        [SerializeField] Material wallMaterial;

        private Caves caves;
        private Mesh wallMesh;

        void Awake()
        {
            caves = GetComponent<Caves>();
            wallMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        }

        private void Start()
        {
            caves.OnCreated += Caves_OnCreated;
        }

        private void Caves_OnCreated()
        {
            foreach (GridWall wall in caves.Walls)
            {
                AddWall(wall);
            }
        }

        void Update()
        {
            // TODO
        }

        private void AddWall(GridWall wall)
        {
            // TODO
        }

        private class BatchOfMatrices
        {
            public Matrix4x4[] Matrices { get; private set; } = new Matrix4x4[MAX_COUNT];
            public const int MAX_COUNT = 1023; // Unity's DrawMeshInstanced has a limit of 1023 instances per call

            // TODO - start here next time
        }

    }
}
