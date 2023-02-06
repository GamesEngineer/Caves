using Lesson;
using System;
using System.Collections;
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
        [SerializeField] Material floorCeilingMaterial;
        [SerializeField] UnityEngine.UI.Image progressImage;

        [SerializeField] Light lightPrefab;

        private CaveSystem caves;

        private const float COROUTINE_TIME_SLICE = 0.05f; // seconds

        private void Awake()
        {
            caves = GetComponent<CaveSystem>();
        }

        private void Start()
        {
            caves.OnCreated += Caves_OnCreated;
        }

        private void Caves_OnCreated()
        {
            StartCoroutine(CreateWalls(caves.Walls));
            Illuminate();
        }

        public void CreateMazeWalls(IReadOnlyCollection<GridWall> walls)
        {
            foreach (GridWall wall in walls)
            {
                // HACK to see the interior walls
                if (wall.faceAxis == FaceAxis.DownUp) continue;
                CreateWallObject(wall, transform);
            }
        }

        public IEnumerator CreateWalls(IReadOnlyCollection<GridWall> walls)
        {
            int p = 0;
            progressImage.color = Color.blue;
            progressImage.fillAmount = 0f;
            yield return null;

            float time = Time.realtimeSinceStartup;
            foreach (GridWall wall in walls)
            {
                progressImage.fillAmount = p++ / (float)walls.Count;
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

            progressImage.fillAmount = 1f;
            yield return null;
            progressImage.gameObject.SetActive(false);
        }

        private void Illuminate()
        {
            for (int roomIndex = 0; roomIndex < caves.RoomCount; roomIndex++)
            {
                Vector3Int roomCoordinates = caves.GetRoomCenter(roomIndex);
                Vector3Int coordinates = roomCoordinates.Step(Direction.Up);
                Vector3 position = GetCellFacePosition(coordinates, Direction.None);
                Light light = Instantiate(lightPrefab, position, Quaternion.identity);
                light.transform.parent = transform;
                light.name = $"Light {coordinates}";
            }
        }
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

        public Vector3 GetWallPosition(GridWall wall)
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
            for (int i = 0; i < wallParent.childCount; i++)
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
    }
}
