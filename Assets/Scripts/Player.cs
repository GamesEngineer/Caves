using GameU;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField, Range(0.1f, 10f)] float moveSpeed = 5f;
    [SerializeField, Range(0.1f, 10f)] float turnSpeed = 2f;
    [SerializeField, Range(0.1f, 10f)] float lookSensitivity = 5f;
    [SerializeField] CaveSystem caves;
    [SerializeField] Transform face;

    Vector3Int targetCoordinates;
    Vector3 targetPosition;
    Direction targetDirection = Direction.North;
    Vector3 targetForward;
    Vector3 lookForward = Vector3.forward;
    float yaw;
    float pitch;

    private void Awake()
    {
        targetPosition = transform.position;
        targetForward = transform.forward;
    }

    void Start()
    {
        caves.OnCreated += Caves_OnCreated;
    }

    private void Caves_OnCreated()
    {
        Vector3Int c = caves.GetRoomCenter(0);
        if (caves.TryGetFloorPosition(ref c, out Vector3 floorPosition))
        {
            targetCoordinates = c;
            targetPosition = floorPosition;
            transform.position = floorPosition;
            targetDirection = Direction.North;
            targetForward = targetCoordinates.Step(targetDirection) - targetCoordinates;
        }
    }

    void Update()
    {
        if (Vector3.Distance(targetPosition, transform.position) < 0.01f &&
            Vector3.Angle(targetForward, transform.forward) < 1f)
        {
            if (Input.GetKeyDown(KeyCode.A)) TurnLeft();
            if (Input.GetKeyDown(KeyCode.D)) TurnRight();
            if (Input.GetKeyDown(KeyCode.W)) StepForward();
            if (Input.GetKeyDown(KeyCode.S)) StepBackward();
        }

        float maxTurnDelta = turnSpeed * Time.deltaTime * Mathf.PI;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        transform.forward = Vector3.RotateTowards(transform.forward, targetForward, maxTurnDelta, 1f);

        if (Input.GetMouseButton(0))
        {
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");
            yaw += mx * lookSensitivity;
            pitch -= my * lookSensitivity;
            yaw = Mathf.Clamp(yaw, -45f, 45f);
            pitch = Mathf.Clamp(pitch, -45f, 45f);
            lookForward = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
        }
        else
        {
            yaw = 0f;
            pitch = 0f;
            lookForward = Vector3.RotateTowards(lookForward, Vector3.forward, maxTurnDelta, 1f);
        }
        face.forward = transform.rotation * lookForward;
    }

    private bool StepForward()
    {
        Vector3Int c = targetCoordinates.Step(targetDirection);
        if (!caves.TryGetFloorPosition(ref c, out Vector3 floorPosition))
        {
            // If there is a wall in front us, try to climb it
            Vector3Int up = c.Step(Direction.Up);
            c = up;
            if (!caves.TryGetFloorPosition(ref c, out floorPosition))
            {
                return false;
            }
        }
        if (!caves.IsCellOpen(c.Step(Direction.Up)))
        {
            return false;
        }
        targetCoordinates = c;
        targetPosition = floorPosition;
        return true;
    }

    private bool StepBackward()
    {
        Vector3Int c = targetCoordinates.Step(targetDirection.OppositeDirection());
        if (!caves.TryGetFloorPosition(ref c, out Vector3 floorPosition))
        {
            return false;
        }
        targetCoordinates = c;
        targetPosition = floorPosition;
        return true;
    }

    private void TurnLeft()
    {
        targetDirection = targetDirection.TurnLeft();
        targetForward = targetCoordinates.Step(targetDirection) - targetCoordinates;
    }

    private void TurnRight()
    {
        targetDirection = targetDirection.TurnRight();
        targetForward = targetCoordinates.Step(targetDirection) - targetCoordinates;
    }
}
