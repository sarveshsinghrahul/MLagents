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
    private Rigidbody _rb;

    public override void Initialize()
    {
        base.Initialize();
        _rb = GetComponent<Rigidbody>();
        _startPosition = transform.localPosition;
        _targetStartPosition = targetTransform.localPosition;
    }

    public override void OnEpisodeBegin()
    {
        // --- 1. Randomize AGENT Position ---
        // (Keep your existing logic for the agent)
        float agentRandomZ = Random.Range(-6f, 4f);
        transform.localPosition = new Vector3(_startPosition.x, _startPosition.y, agentRandomZ);

        // --- 2. Randomize TARGET Position ---
        // X axis: Random between -17 and 0
        // Z axis: Random between -3 and 0
        float targetRandomX = Random.Range(-17f, 0f);
        float targetRandomZ = Random.Range(-3f, 0f);

        // Apply new position (keeping the original Y height to prevent sinking/floating)
        targetTransform.localPosition = new Vector3(targetRandomX, _targetStartPosition.y, targetRandomZ);

        // --- 3. Reset Physics ---
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Note: For complex mazes, RayPerceptionSensor3D is better than positions
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(targetTransform.localPosition);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];

        float moveSpeed = 5f; // Increased slightly for snappier movement
        Vector3 movement = new Vector3(moveX, 0, moveZ) * moveSpeed * Time.deltaTime;
        _rb.MovePosition(transform.position + movement);

        // --- 1. THE EXISTENTIAL CRISIS PENALTY ---
        // We punish the agent every single step based on total max steps.
        // If MaxSteps is 5000, this is -0.0002 per frame.
        // This forces the agent to solve the maze FAST.
        AddReward(-1f / MaxStep);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = 0;
        continuousActionsOut[1] = 0;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed) continuousActionsOut[0] = 1;
            if (Keyboard.current.aKey.isPressed) continuousActionsOut[0] = -1;
            if (Keyboard.current.wKey.isPressed) continuousActionsOut[1] = 1;
            if (Keyboard.current.sKey.isPressed) continuousActionsOut[1] = -1;
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        // --- 2. THE GOAL REWARD ---
        if (collision.gameObject.TryGetComponent<goal>(out goal goalScript))
        {
            SetReward(10.0f); // Big payout
            EndEpisode();
        }

        // --- 3. THE WALL SLAP (Modified) ---
        // I combined your 'walls' and 'barricade' logic here.
        // Crucial: Do NOT EndEpisode() on wall hit for a maze. 
        // Just give a small slap so it prefers clear paths.
        else if (collision.gameObject.TryGetComponent<walls>(out walls wallScript) ||
                 collision.gameObject.TryGetComponent<barricade>(out barricade barricadeScript))
        {
            AddReward(-0.005f); // Tiny penalty. 
            // Do NOT call EndEpisode(); let it slide along the wall to find the exit.
        }
    }
}