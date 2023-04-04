using System.Collections;
using System.Collections.Generic;
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
            caves = GetComponent<CaveSystem>();
            caveWalls = GetComponent<CaveWalls>();
            caves.OnCreated += Caves_OnCreated;
        }

        private void OnDestroy()
        {
            caves.OnCreated -= Caves_OnCreated;
        }

        //private void Update()
        //{        
        //}

        private void Caves_OnCreated()
        {
            // spawn one enemy in each room
            for (int roomNumber = 0; roomNumber < caves.RoomCount; roomNumber++)
            {
                Vector3Int coords = caves.GetRoomCenter(roomNumber);
                float randomAngle = UnityEngine.Random.value * 360f;
                Vector3 randomDirection = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward * 3f;
                coords.x += Mathf.RoundToInt(randomDirection.x);
                coords.z += Mathf.RoundToInt(randomDirection.z);
                coords.y = caves.FindFloorHeight(coords.x, coords.z);
                Vector3 position = Vector3.zero;
                caveWalls.TryGetFloorPosition(ref coords, out position);
                Enemy enemy = Instantiate<Enemy>(enemyPrefab, position, Quaternion.identity);
                enemy.coords = coords;
            }
        }
    }
}
