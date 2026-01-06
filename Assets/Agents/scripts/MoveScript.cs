using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

public class MoveScript : Agent
{
    [SerializeField] private Transform targetTransform;

    private Vector3 _startPosition;
    private Vector3 _targetStartPosition;

    // 1. Add a variable to track the distance from the last frame
    private float _previousDistance;
    private Rigidbody _rb;

    public override void Initialize()
    {
        base.Initialize(); // Good practice to call base
        _rb = GetComponent<Rigidbody>(); // 2. Get the Rigidbody
        _startPosition = transform.localPosition;
        _targetStartPosition = targetTransform.localPosition;
    }

    public override void OnEpisodeBegin()
    {
        transform.localPosition = _startPosition;
        targetTransform.localPosition = _targetStartPosition;

        // 2. Reset the previous distance at the start of the episode
        _previousDistance = Vector3.Distance(transform.position, targetTransform.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.position);
        sensor.AddObservation(targetTransform.position);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];

        float moveSpeed = 3f;
        // 3. Move using Physics instead of Transform
        Vector3 movement = new Vector3(moveX, 0, moveZ) * moveSpeed * Time.deltaTime;
        _rb.MovePosition(transform.position + movement);

        // --- NEW CODE START ---

        // Calculate current distance
        float distanceToTarget = Vector3.Distance(transform.position, targetTransform.position);

        // Calculate how much we improved (Previous - Current)
        // If result is positive, we got closer. If negative, we moved away.
        float distanceDelta = _previousDistance - distanceToTarget;

        // Add a reward based on that improvement. 
        // We implicitly punish moving away and reward moving closer.
        AddReward(distanceDelta);

        // Update previous distance for the next step
        _previousDistance = distanceToTarget;

        // --- NEW CODE END ---

        // Existing time penalty
        AddReward(-0.01f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;

        // Reset to 0
        continuousActionsOut[0] = 0;
        continuousActionsOut[1] = 0;

        // Use Keyboard.current to check keys directly
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed) continuousActionsOut[0] = 1; // Right
            if (Keyboard.current.aKey.isPressed) continuousActionsOut[0] = -1; // Left

            if (Keyboard.current.wKey.isPressed) continuousActionsOut[1] = 1; // Up
            if (Keyboard.current.sKey.isPressed) continuousActionsOut[1] = -1; // Down
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.TryGetComponent<goal>(out goal goal))
        {
            SetReward(10.0f);
            EndEpisode();
        }
        else if (collision.gameObject.TryGetComponent<walls>(out walls walls))
        {
            SetReward(-2.0f);
            EndEpisode();
        }
        else if (collision.gameObject.TryGetComponent<barricade>(out barricade barricade))
        {
            SetReward(-0.2f);
            // If you want it to bounce off, Physics does that automatically now.
            // If you want it to RESET on barricade hit, add EndEpisode() here.
        }
    }
}