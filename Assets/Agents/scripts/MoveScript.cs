using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class MoveScript : Agent
{
    [Header("Dungeon Setup")]
    public Transform[] spawnPoints;
    public GameObject[] allGoals;

    [Header("Door Setup")]
    public Transform[] roomDoors;

    [Header("Camera Setup")]
    public DungeonCamera cameraController; // <--- NEW: Drag Main Camera here!

    [Header("Training Settings")]
    public float spawnRadius = 3f;
    public float turnSpeed = 15f;

    private int _currentRoomIndex = 0;
    private Rigidbody _rb;
    private bool _isTransitioning = false;

    private List<Vector3> _initialDoorPositions = new List<Vector3>();
    private List<Vector3> _initialGoalPositions = new List<Vector3>();

    public override void Initialize()
    {
        base.Initialize();
        _rb = GetComponent<Rigidbody>();
        _currentRoomIndex = 0;

        _initialDoorPositions.Clear();
        foreach (Transform door in roomDoors)
        {
            if (door != null) _initialDoorPositions.Add(door.localPosition);
            else _initialDoorPositions.Add(Vector3.zero);
        }

        _initialGoalPositions.Clear();
        foreach (GameObject goal in allGoals)
        {
            if (goal != null) _initialGoalPositions.Add(goal.transform.position);
            else _initialGoalPositions.Add(Vector3.zero);
        }
    }

    public override void OnEpisodeBegin()
    {
        _isTransitioning = false;
        _rb.isKinematic = false;

        // 1. RANDOMIZE AGENT
        if (spawnPoints.Length > _currentRoomIndex)
        {
            transform.position = GetRandomPosAt(_currentRoomIndex);
            transform.rotation = Quaternion.Euler(0, 90f, 0);
        }

        // --- NEW: SNAP CAMERA TO CURRENT ROOM ON RESET ---
        if (cameraController != null)
        {
            cameraController.SnapToRoom(_currentRoomIndex);
        }

        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        // 2. RANDOMIZE GOALS
        for (int i = 0; i < allGoals.Length; i++)
        {
            if (allGoals[i] != null && i < _initialGoalPositions.Count)
            {
                allGoals[i].SetActive(true);
                Vector3 basePos = _initialGoalPositions[i];
                float randomZ = Random.Range(-spawnRadius, spawnRadius);
                Vector3 newGoalPos = basePos + new Vector3(0, 0, randomZ);
                allGoals[i].transform.position = newGoalPos;
            }
        }

        // 3. RESET DOORS
        if (roomDoors != null)
        {
            for (int i = 0; i < roomDoors.Length; i++)
            {
                if (roomDoors[i] != null && i < _initialDoorPositions.Count)
                {
                    roomDoors[i].localPosition = _initialDoorPositions[i];
                }
            }
        }
    }

    // (Keep GetRandomPosAt, CollectObservations, OnActionReceived, OnCollisionEnter EXACTLY THE SAME)
    // I am omitting them here to save space, but DO NOT DELETE THEM from your script.

    private Vector3 GetRandomPosAt(int roomIndex)
    {
        if (roomIndex >= spawnPoints.Length) return transform.position;
        Transform centerPoint = spawnPoints[roomIndex];
        float randomX = Random.Range(-spawnRadius, spawnRadius);
        float randomZ = Random.Range(-spawnRadius, spawnRadius);
        Vector3 randomPos = centerPoint.position + new Vector3(randomX, 0, randomZ);
        randomPos.y = centerPoint.position.y;
        return randomPos;
    }

    public override void CollectObservations(VectorSensor sensor) { sensor.AddObservation(_currentRoomIndex); }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_isTransitioning) return;
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        float moveSpeed = 6f;
        Vector3 direction = new Vector3(moveX, 0, moveZ);
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            Quaternion nextRotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            _rb.MoveRotation(nextRotation);
        }
        _rb.MovePosition(transform.position + (direction * moveSpeed * Time.deltaTime));
        AddReward(-1f / MaxStep);
    }

    public void OnTriggerEnter(Collider other)
    {
        if (_isTransitioning) return;

        if (other.CompareTag("goal") || other.CompareTag("Checkpoint"))
        {
            other.gameObject.SetActive(false);
            AddReward(1.0f);

            int nextRoom = _currentRoomIndex + 1;

            if (nextRoom >= spawnPoints.Length)
            {
                AddReward(5.0f);
                _currentRoomIndex = 0;
                EndEpisode();
            }
            else
            {
                Transform doorToOpen = null;
                Vector3 closedPos = Vector3.zero;

                if (roomDoors != null && _currentRoomIndex < roomDoors.Length)
                {
                    doorToOpen = roomDoors[_currentRoomIndex];
                    closedPos = _initialDoorPositions[_currentRoomIndex];
                }

                StartCoroutine(MoveAgentThroughDoor(spawnPoints[nextRoom], doorToOpen, closedPos));
            }
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (_isTransitioning) return;
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-0.02f);
            transform.position = GetRandomPosAt(_currentRoomIndex);
            transform.rotation = Quaternion.Euler(0, 90f, 0);
            _rb.linearVelocity = Vector3.zero;
        }
    }

    IEnumerator MoveAgentThroughDoor(Transform targetDestination, Transform door, Vector3 closedLocalPos)
    {
        _isTransitioning = true;
        _rb.isKinematic = true;

        // --- NEW: TELL CAMERA TO MOVE TO NEXT ROOM ---
        // We calculate next room index as current + 1
        if (cameraController != null)
        {
            cameraController.MoveToRoom(_currentRoomIndex + 1);
        }

        // 1. OPEN DOOR
        if (door != null)
        {
            Vector3 startLocal = closedLocalPos;
            Vector3 endLocal = startLocal + Vector3.up * 4.0f;
            float openDuration = 0.5f;
            float openTime = 0f;
            while (openTime < openDuration)
            {
                door.localPosition = Vector3.Lerp(startLocal, endLocal, openTime / openDuration);
                openTime += Time.deltaTime;
                yield return null;
            }
            door.localPosition = endLocal;
        }

        // 2. MOVE AGENT
        float moveDuration = 1.5f;
        float moveTime = 0f;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 targetPos = new Vector3(targetDestination.position.x, startPos.y, targetDestination.position.z);
        Quaternion targetRot = Quaternion.Euler(0, 90f, 0);

        while (moveTime < moveDuration)
        {
            float t = moveTime / moveDuration;
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            moveTime += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos;
        transform.rotation = targetRot;

        // 3. CLOSE DOOR
        if (door != null)
        {
            float closeDuration = 0.5f;
            float closeTime = 0f;
            Vector3 currentLocal = door.localPosition;
            while (closeTime < closeDuration)
            {
                door.localPosition = Vector3.Lerp(currentLocal, closedLocalPos, closeTime / closeDuration);
                closeTime += Time.deltaTime;
                yield return null;
            }
            door.localPosition = closedLocalPos;
        }

        _currentRoomIndex++;
        _rb.isKinematic = false;
        _isTransitioning = false;
    }

    // (Keep Heuristic exactly the same)
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = 0; continuousActionsOut[1] = 0;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed) continuousActionsOut[0] = 1;
            if (Keyboard.current.aKey.isPressed) continuousActionsOut[0] = -1;
            if (Keyboard.current.wKey.isPressed) continuousActionsOut[1] = 1;
            if (Keyboard.current.sKey.isPressed) continuousActionsOut[1] = -1;
        }
    }
}