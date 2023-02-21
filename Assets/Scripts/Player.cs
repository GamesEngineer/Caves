using UnityEngine;

namespace GameU
{
    public class Player : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 10f)] float moveSpeed = 5f;
        [SerializeField, Range(0.1f, 10f)] float turnSpeed = 2f;
        [SerializeField, Range(0.1f, 10f)] float lookSensitivity = 5f;
        [SerializeField] CaveSystem caves;
        [SerializeField] CaveWalls caveWalls;
        [SerializeField] Transform face;

        public Vector3Int CellCoordinates { get; private set; }
        public Direction ForwardDirection { get; private set; } = Direction.North;
        public Vector3 FaceForwardVector { get; private set; }
        public Vector3 LookForwardVector { get; private set; } = Vector3.forward;
        public float LookYawAngle { get; private set; }
        public float LookPitchAngle { get; private set; }
        public bool CanStepForward { get; private set; }

        Vector3 targetPosition;

        private void Awake()
        {
            targetPosition = transform.position;
            FaceForwardVector = transform.forward;
        }

        void Start()
        {
            caves.OnCreated += Caves_OnCreated;
        }

        private void Caves_OnCreated()
        {
            Vector3Int c = caves.GetRoomCenter(0);
            if (caveWalls.TryGetFloorPosition(ref c, out Vector3 floorPosition))
            {
                CellCoordinates = c;
                transform.position = floorPosition;
                targetPosition = floorPosition;
                ForwardDirection = Direction.North;
                FaceForwardVector = CellCoordinates.Step(ForwardDirection) - CellCoordinates;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) QuitGame();

            if (Vector3.Distance(targetPosition, transform.position) < 0.01f &&
                Vector3.Angle(FaceForwardVector, transform.forward) < 1f)
            {
                if (Input.GetKeyDown(KeyCode.Q)) TurnLeft();
                if (Input.GetKeyDown(KeyCode.E)) TurnRight();
                if (Input.GetKeyDown(KeyCode.A)) StepLeft();
                if (Input.GetKeyDown(KeyCode.D)) StepRight();
                if (Input.GetKeyDown(KeyCode.W)) StepForward();
                if (Input.GetKeyDown(KeyCode.S)) StepBackward();
            }

            float maxTurnDelta = turnSpeed * Time.deltaTime * Mathf.PI;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            transform.forward = Vector3.RotateTowards(transform.forward, FaceForwardVector, maxTurnDelta, 1f);

            if (Input.GetMouseButton(0))
            {
                float mx = Input.GetAxis("Mouse X");
                float my = Input.GetAxis("Mouse Y");
                LookYawAngle += mx * lookSensitivity;
                LookPitchAngle -= my * lookSensitivity;
                LookYawAngle = Mathf.Clamp(LookYawAngle, -45f, 45f);
                LookPitchAngle = Mathf.Clamp(LookPitchAngle, -45f, 45f);
                LookForwardVector = Quaternion.Euler(LookPitchAngle, LookYawAngle, 0f) * Vector3.forward;
            }
            else
            {
                LookYawAngle = 0f;
                LookPitchAngle = 0f;
                LookForwardVector = Vector3.RotateTowards(LookForwardVector, Vector3.forward, maxTurnDelta, 1f);
            }
            face.forward = transform.rotation * LookForwardVector;

            Vector3Int oneStepForward = CellCoordinates.Step(ForwardDirection);
            CanStepForward = caves.IsCellOpen(oneStepForward.Step(Direction.Up)); // TODO FIXME - not valid in all cases
        }

        private bool StepForward()
        {
            Vector3Int c = CellCoordinates.Step(ForwardDirection);

            if (!caveWalls.TryGetFloorPosition(ref c, out Vector3 floorPosition))
            {
                // If there is a ledge or wall in front us, try to climb it
                c = c.Step(Direction.Up);

                if (!caveWalls.TryGetFloorPosition(ref c, out floorPosition))
                {
                    c = CellCoordinates.Step(Direction.Up);
                    floorPosition = caveWalls.GetCellFacePosition(c, Direction.None);
                }
            }

            if (!caves.IsStandingSpaceOpen(c)) return false;

            CellCoordinates = c;
            targetPosition = floorPosition;
            return true;
        }

        private bool StepBackward()
        {
            Vector3Int c;
            Vector3 floorPosition;
            if (caves.IsCellOpen(CellCoordinates.Step(Direction.Down)))
            {
                c = CellCoordinates.Step(Direction.Down);
                floorPosition = caveWalls.GetCellFacePosition(c, Direction.None);
            }
            else
            {
                c = CellCoordinates.Step(ForwardDirection.OppositeDirection());
                if (!caves.IsStandingSpaceOpen(c)) return false;

                if (!caveWalls.TryGetFloorPosition(ref c, out floorPosition))
                {
                    return false;
                }
            }

            CellCoordinates = c;
            targetPosition = floorPosition;
            return true;
        }

        private bool StepLeft()
        {
            Vector3Int c = CellCoordinates.Step(ForwardDirection.TurnLeft());
            if (!caves.IsStandingSpaceOpen(c)) return false;

            Vector3 floorPosition;
            if (caves.IsCellOpen(CellCoordinates.Step(Direction.Down)) && !caves.IsCellOpen(c.Step(ForwardDirection)))
            {
                floorPosition = caveWalls.GetCellFacePosition(c, Direction.None);
            }
            else if (!caveWalls.TryGetFloorPosition(ref c, out floorPosition))
            {
                return false;
            }

            CellCoordinates = c;
            targetPosition = floorPosition;
            return true;
        }

        private bool StepRight()
        {
            Vector3Int c = CellCoordinates.Step(ForwardDirection.TurnRight());
            if (!caves.IsStandingSpaceOpen(c)) return false;

            Vector3 floorPosition;
            if (caves.IsCellOpen(CellCoordinates.Step(Direction.Down)) && !caves.IsCellOpen(c.Step(ForwardDirection)))
            {
                floorPosition = caveWalls.GetCellFacePosition(c, Direction.None);
            }
            else if (!caveWalls.TryGetFloorPosition(ref c, out floorPosition))
            {
                return false;
            }

            CellCoordinates = c;
            targetPosition = floorPosition;
            return true;
        }

        private bool TurnLeft()
        {
            if (caves.IsCellOpen(CellCoordinates.Step(Direction.Down))) return false;

            ForwardDirection = ForwardDirection.TurnLeft();
            FaceForwardVector = CellCoordinates.Step(ForwardDirection) - CellCoordinates;
            return true;
        }

        private bool TurnRight()
        {
            if (caves.IsCellOpen(CellCoordinates.Step(Direction.Down))) return false;

            ForwardDirection = ForwardDirection.TurnRight();
            FaceForwardVector = CellCoordinates.Step(ForwardDirection) - CellCoordinates;
            return true;
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        }
    }
}
