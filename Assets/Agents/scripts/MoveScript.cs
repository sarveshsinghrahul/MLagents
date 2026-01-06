using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class MoveScript : Agent
{
    [SerializeField] private Transform targetTransform;

    private Vector3 _startPosition;
    private Vector3 _targetStartPosition;

    // Initialize is called once when the game starts (better than Start for ML-Agents)
    public override void Initialize()
    {
        _startPosition = transform.localPosition;
        _targetStartPosition = targetTransform.localPosition;
    }

    public override void OnEpisodeBegin()
    {
        // Reset to the "memorized" positions, not (0,0,0)
        transform.localPosition = _startPosition;
        targetTransform.localPosition = _targetStartPosition;
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

        float moveSpeed = 2f;
        transform.position += new Vector3(moveX, 0, moveZ) * moveSpeed * Time.deltaTime;
        AddReward(-0.01f);

    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Horizontal");
        continuousActionsOut[1] = Input.GetAxis("Vertical");
    }
    public void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent<goal>(out goal goal))
        {
            SetReward(10.0f);
            EndEpisode();
        }
        else if(other.TryGetComponent<walls>(out walls walls))
        {
            SetReward(-2.0f);
            EndEpisode();
        }
        else if(other.TryGetComponent<barricade>(out barricade barricade))
        {
            SetReward(-2f);
            EndEpisode();
        }
    }
}
