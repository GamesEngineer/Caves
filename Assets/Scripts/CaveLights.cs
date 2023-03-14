using UnityEngine;

namespace GameU
{
    public class CaveLights : MonoBehaviour
    {
        [SerializeField] Light lightPrefab;

        private CaveSystem caves;
        private CaveWalls caveWalls;

        private void Awake()
        {
            caves = GetComponent<CaveSystem>();
            caveWalls = GetComponent<CaveWalls>();
        }

        private void Start()
        {
            caves.OnCreated += Caves_OnCreated;
        }

        private void Caves_OnCreated()
        {
            Illuminate();
        }

        private void Illuminate()
        {
            for (int roomIndex = 0; roomIndex < caves.RoomCount; roomIndex++)
            {
                Vector3Int roomCoordinates = caves.GetRoomCenter(roomIndex);
                Vector3Int coordinates = roomCoordinates.Step(Direction.Up);
                Vector3 position = caveWalls.GetCellPosition(coordinates);
                Light light = Instantiate(lightPrefab, position, Quaternion.identity);
                light.transform.parent = transform;
                light.name = $"Light {coordinates}";
                light.range = Mathf.Max(caves.RoomSize.x, caves.RoomSize.y, caves.RoomSize.z);
            }
        }
    }
}
