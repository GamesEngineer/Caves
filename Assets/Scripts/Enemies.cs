using UnityEngine;

namespace GameU
{
    public class Enemies : MonoBehaviour
    {
        [SerializeField] Enemy enemyPrefab;

        private CaveSystem caves;
        private CaveWalls caveWalls;

        private void Start()
        {
            caves = FindObjectOfType<CaveSystem>();
            caveWalls = FindObjectOfType<CaveWalls>();
            caves.OnCreated += Caves_OnCreated;
        }

        private void OnDestroy()
        {
            caves.OnCreated -= Caves_OnCreated;
        }

        private void Caves_OnCreated()
        {
            // spawn one enemy in each room
            for (int roomNumber = 0; roomNumber < caves.RoomCount; roomNumber++)
            {
                Vector3Int coords = caves.GetRoomCenter(roomNumber);
                float randomAngle = UnityEngine.Random.value * 360f;
                Vector3 randomDirection = Quaternion.Euler(0f, randomAngle, 0f) * (Vector3.forward * 3f);
                coords.x += Mathf.RoundToInt(randomDirection.x);
                coords.z += Mathf.RoundToInt(randomDirection.z);
                coords.Clamp(Vector3Int.zero, caves.GridSize - Vector3Int.one);
                coords.y = caves.FindFloorHeight(coords.x, coords.z);
                Vector3 position = caveWalls.GetCellFacePosition(coords, Direction.Down);
                position += Vector3.up * caveWalls.WallThickness;
                Enemy enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
                enemy.Coordinates = coords;
                enemy.name = $"Enemy {roomNumber}";
            }
        }
    }
}
