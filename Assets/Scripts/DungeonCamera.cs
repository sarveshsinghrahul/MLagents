using UnityEngine;

public class DungeonCamera : MonoBehaviour
{
    [Header("Camera Positions per Room")]
    public Transform[] roomViews; // Drag empty GameObjects here for each room's view

    public float transitionSpeed = 2.0f;

    private Vector3 _targetPos;
    private Quaternion _targetRot;

    void Start()
    {
        // Initialize to the current position
        if (roomViews.Length > 0)
        {
            _targetPos = roomViews[0].position;
            _targetRot = roomViews[0].rotation;
        }
        else
        {
            _targetPos = transform.position;
            _targetRot = transform.rotation;
        }
    }

    void Update()
    {
        // Smoothly move the camera to the target position
        transform.position = Vector3.Lerp(transform.position, _targetPos, transitionSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRot, transitionSpeed * Time.deltaTime);
    }

    // Call this to smoothly fly to a new room
    public void MoveToRoom(int roomIndex)
    {
        if (roomIndex < roomViews.Length)
        {
            _targetPos = roomViews[roomIndex].position;
            _targetRot = roomViews[roomIndex].rotation;
        }
    }

    // Call this to instantly snap (e.g., on reset)
    public void SnapToRoom(int roomIndex)
    {
        if (roomIndex < roomViews.Length)
        {
            _targetPos = roomViews[roomIndex].position;
            _targetRot = roomViews[roomIndex].rotation;

            transform.position = _targetPos;
            transform.rotation = _targetRot;
        }
    }
}