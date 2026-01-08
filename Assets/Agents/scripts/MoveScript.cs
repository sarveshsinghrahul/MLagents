using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;
using UnityEngine.InputSystem; // <--- FIX 2: ADD THIS

public class MoveScript : Agent
{
    [Header("Dungeon Setup")]
    public Transform[] spawnPoints;
    public GameObject[] allGoals;

    private int _currentRoomIndex = 0;
    private Rigidbody _rb;
    private bool _isTransitioning = false;

    public override void Initialize()
    {
        base.Initialize();
        _rb = GetComponent<Rigidbody>();
        _currentRoomIndex = 0;
    }

    public override void OnEpisodeBegin()
    {
        // 1. DO NOT reset _currentRoomIndex here. 
        // We want to keep our progress if we just ran out of time.

        _isTransitioning = false;
        _rb.isKinematic = false;

        // 2. Spawn at the CURRENT room's spawn point
        if (spawnPoints.Length > _currentRoomIndex)
        {
            Transform currentSpawn = spawnPoints[_currentRoomIndex];
            transform.position = currentSpawn.position;
            transform.rotation = currentSpawn.rotation;
        }

        // 3. Reset Physics
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        // 4. Ensure goals are active so we can try again
        foreach (GameObject g in allGoals) { if (g) g.SetActive(true); }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(_currentRoomIndex);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_isTransitioning) return;

        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        float moveSpeed = 6f;

        Vector3 movement = new Vector3(moveX, 0, moveZ) * moveSpeed * Time.deltaTime;
        _rb.MovePosition(transform.position + movement);

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
                _currentRoomIndex = 0; // Reset for next episode
                EndEpisode();
            }
            else
            {
                StartCoroutine(MoveAgentThroughDoor(spawnPoints[nextRoom]));
            }
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (_isTransitioning) return;

        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-0.02f);
            Transform retryPoint = spawnPoints[_currentRoomIndex];
            transform.position = retryPoint.position;
            transform.rotation = retryPoint.rotation;

            // --- FIX 1: USE LINEARVELOCITY HERE TOO ---
            _rb.linearVelocity = Vector3.zero;
        }
    }

    IEnumerator MoveAgentThroughDoor(Transform targetDestination)
    {
        _isTransitioning = true;
        _rb.isKinematic = true;

        float duration = 1.5f;
        float elapsedTime = 0f;
        Vector3 startPos = transform.position;

        while (elapsedTime < duration)
        {
            transform.position = Vector3.Lerp(startPos, targetDestination.position, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetDestination.position;
        _currentRoomIndex++;
        _rb.isKinematic = false;
        _isTransitioning = false;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = 0; continuousActionsOut[1] = 0;

        // This 'Keyboard' class now works because we added 'using UnityEngine.InputSystem;'
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed) continuousActionsOut[0] = 1;
            if (Keyboard.current.aKey.isPressed) continuousActionsOut[0] = -1;
            if (Keyboard.current.wKey.isPressed) continuousActionsOut[1] = 1;
            if (Keyboard.current.sKey.isPressed) continuousActionsOut[1] = -1;
        }
    }
}