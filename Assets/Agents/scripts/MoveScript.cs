using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;

public class MoveScript : Agent
{
    [Header("Dungeon Setup")]
    public Transform[] spawnPoints;
    public GameObject[] allGoals;

    [Header("Door Setup")]
    public Transform[] roomDoors;

    [Header("UI Setup")]
    public TextMeshPro[] scoreBoards;

    [Header("Camera Setup")]
    public DungeonCamera cameraController;

    [Header("Training Settings")]
    public float spawnRadius = 3f;
    public float turnSpeed = 15f;
    public int requiredWins = 10;

    private int _currentRoomIndex = 0;
    private int _currentWins = 0;
    private Rigidbody _rb;
    private bool _isTransitioning = false;

    private List<Vector3> _initialDoorPositions = new List<Vector3>();
    private List<Vector3> _initialGoalPositions = new List<Vector3>();

    public override void Initialize()
    {
        base.Initialize();
        _rb = GetComponent<Rigidbody>();
        _currentRoomIndex = 0;
        _currentWins = 0;

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
        _currentWins = 0;

        UpdateScoreBoard(); // Initial update

        ResetAgentAndGoalInRoom(_currentRoomIndex);

        if (cameraController != null) cameraController.SnapToRoom(_currentRoomIndex);

        if (roomDoors != null)
        {
            for (int i = 0; i < roomDoors.Length; i++)
            {
                if (roomDoors[i] != null && i < _initialDoorPositions.Count)
                    roomDoors[i].localPosition = _initialDoorPositions[i];
            }
        }
    }

    // --- NEW: UPDATE TIMER EVERY PHYSICS STEP ---
    public void FixedUpdate()
    {
        // Only update if we are in the middle of an episode
        if (!_isTransitioning && MaxStep > 0)
        {
            UpdateScoreBoard();
        }
    }

    private void UpdateScoreBoard()
    {
        if (scoreBoards != null && _currentRoomIndex < scoreBoards.Length && scoreBoards[_currentRoomIndex] != null)
        {
            // 1. Calculate Time
            float stepsRemaining = MaxStep - StepCount;
            float secondsRemaining = stepsRemaining * Time.fixedDeltaTime;
            if (secondsRemaining < 0) secondsRemaining = 0;

            // 2. New Text Format
            // "\n" creates a new line so it looks cleaner
            scoreBoards[_currentRoomIndex].text = 
                $"To Open the Gate:\nScore {_currentWins}/{requiredWins} | Time Remaining: {secondsRemaining:F0}s";

            // 3. Color Logic (Same as before)
            if (secondsRemaining <= 10f) 
            {
                scoreBoards[_currentRoomIndex].color = Color.red; 
            }
            else if (_currentWins >= requiredWins) 
            {
                scoreBoards[_currentRoomIndex].color = Color.green; 
            }
            else 
            {
                scoreBoards[_currentRoomIndex].color = Color.white; 
            }
        }
    }

    private void ResetAgentAndGoalInRoom(int roomIdx)
    {
        if (spawnPoints.Length > roomIdx)
        {
            transform.position = GetRandomPosAt(roomIdx);
            transform.rotation = Quaternion.Euler(0, 90f, 0);
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        if (allGoals.Length > roomIdx && allGoals[roomIdx] != null)
        {
            allGoals[roomIdx].SetActive(true);
            Vector3 basePos = _initialGoalPositions[roomIdx];
            float randomZ = Random.Range(-spawnRadius, spawnRadius);
            allGoals[roomIdx].transform.position = basePos + new Vector3(0, 0, randomZ);
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (_isTransitioning) return;

        if (other.CompareTag("goal") || other.CompareTag("Checkpoint"))
        {
            _currentWins++;
            // UpdateScoreBoard will happen automatically next frame in FixedUpdate, 
            // but we can call it here for instant feedback if we want.
            UpdateScoreBoard();

            if (_currentWins >= requiredWins)
            {
                other.gameObject.SetActive(false);
                AddReward(5.0f);

                int nextRoom = _currentRoomIndex + 1;
                if (nextRoom >= spawnPoints.Length)
                {
                    AddReward(10.0f);
                    _currentRoomIndex = 0;
                    EndEpisode();
                }
                else
                {
                    _currentWins = 0;
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
            else
            {
                AddReward(1.0f);
                ResetAgentAndGoalInRoom(_currentRoomIndex);
            }
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (_isTransitioning) return;
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-0.02f);
            ResetAgentAndGoalInRoom(_currentRoomIndex);
        }
    }

    // ... (Keep the rest: CollectObservations, OnActionReceived, MoveAgentThroughDoor, GetRandomPosAt, Heuristic) ...

    // --- PASTE REMAINING STANDARD FUNCTIONS BELOW ---
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

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(_currentRoomIndex);
        sensor.AddObservation(transform.forward);
        sensor.AddObservation(_rb.linearVelocity.x);
        sensor.AddObservation(_rb.linearVelocity.z);
        sensor.AddObservation(requiredWins - _currentWins);
    }

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

    IEnumerator MoveAgentThroughDoor(Transform targetDestination, Transform door, Vector3 closedLocalPos)
    {
        _isTransitioning = true;
        _rb.isKinematic = true;
        if (cameraController != null) cameraController.MoveToRoom(_currentRoomIndex + 1);

        if (door != null)
        {
            Vector3 startLocal = closedLocalPos;
            Vector3 endLocal = startLocal + Vector3.up * 4.0f;
            float openDuration = 0.5f; float openTime = 0f;
            while (openTime < openDuration)
            {
                door.localPosition = Vector3.Lerp(startLocal, endLocal, openTime / openDuration);
                openTime += Time.deltaTime; yield return null;
            }
            door.localPosition = endLocal;
        }

        float moveDuration = 1.5f; float moveTime = 0f;
        Vector3 startPos = transform.position; Quaternion startRot = transform.rotation;
        Vector3 targetPos = new Vector3(targetDestination.position.x, startPos.y, targetDestination.position.z);
        Quaternion targetRot = Quaternion.Euler(0, 90f, 0);
        while (moveTime < moveDuration)
        {
            float t = moveTime / moveDuration;
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            moveTime += Time.deltaTime; yield return null;
        }
        transform.position = targetPos; transform.rotation = targetRot;

        if (door != null)
        {
            float closeDuration = 0.5f; float closeTime = 0f;
            Vector3 currentLocal = door.localPosition;
            while (closeTime < closeDuration)
            {
                door.localPosition = Vector3.Lerp(currentLocal, closedLocalPos, closeTime / closeDuration);
                closeTime += Time.deltaTime; yield return null;
            }
            door.localPosition = closedLocalPos;
        }

        _currentRoomIndex++;

        // Manual update to reset color/text immediately on room change
        UpdateScoreBoard();

        _rb.isKinematic = false;
        _isTransitioning = false;
    }

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