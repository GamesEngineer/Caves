using UnityEngine;

namespace GameU
{
    public class Enemy : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 10f)] float moveSpeed = 5f;
        [SerializeField, Range(0.1f, 10f)] float turnSpeed = 2f;
        [SerializeField, Range(0, 10)] int maxChaseDistance = 6;

        public Vector3Int Coordinates { get; set; }
        public Vector3 FaceForwardVector { get; private set; }

        Vector3 targetPosition;
        int flipFlop;
        Player player;
        CaveWalls caveWalls;

        private void Awake()
        {
            targetPosition = transform.position;
            FaceForwardVector = transform.forward;
        }

        private void Start()
        {
            player = FindObjectOfType<Player>();
            player.OnStepTaken += StepTowardsPlayer;
            caveWalls = FindObjectOfType<CaveWalls>();
        }

        private void Update()
        {
            float maxTurnDelta = turnSpeed * Time.deltaTime * Mathf.PI;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            transform.forward = Vector3.RotateTowards(transform.forward, FaceForwardVector, maxTurnDelta, 1f);
        }

        public void StepTowardsPlayer()
        {
            Direction direction = Coordinates.GetLateralDirection(player.CellCoordinates);
            // turn towards step direction
            switch (direction)
            {
                case Direction.North: FaceForwardVector = Vector3.forward; break;
                case Direction.South: FaceForwardVector = Vector3.back; break;
                case Direction.East: FaceForwardVector = Vector3.right; break;
                case Direction.West: FaceForwardVector = Vector3.left; break;
                default: break;
            }

            flipFlop = 1 - flipFlop;
            if (flipFlop == 0) return;

            if (Coordinates == player.CellCoordinates || Vector3Int.Distance(Coordinates, player.CellCoordinates) > maxChaseDistance)
            {
                return;
            }

            Vector3Int c = Coordinates.Step(direction);

            if (!caveWalls.TryGetFloorPosition(ref c, out Vector3 position))
            {
                c = c.Step(Direction.Up, 2);
                if (!caveWalls.TryGetFloorPosition(ref c, out position))
                {
                    return;
                }
            }
            Coordinates = c;
            targetPosition = position;
        }
    }
}
