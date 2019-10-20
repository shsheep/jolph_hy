﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAgents;

[RequireComponent(typeof(JointDriveController))] // Required to set joint forces
public class CrawlerAgent : Agent
{
    [Header("Target To Walk Towards")] [Space(10)]
    public Transform target;
    public Transform obstacle;

    public Transform ground;
    public bool detectTargets;
    public bool avoidObstacles;
    public bool respawnTargetWhenTouched;
    public float targetSpawnRadius;

    [Header("Body Parts")] [Space(10)] public Transform body;
    public Transform leg0Upper;
    public Transform leg0Lower;
    public Transform leg1Upper;
    public Transform leg1Lower;
    public Transform leg2Upper;
    public Transform leg2Lower;
    public Transform leg3Upper;
    public Transform leg3Lower;

    [Header("Joint Settings")] [Space(10)] JointDriveController jdController;
    Vector3 dirToTarget;
    float movingTowardsDot;
    float facingDot;

    [Header("Reward Functions To Use")] [Space(10)]
    public bool rewardMovingTowardsTarget; // Agent should move towards target

    public bool rewardFacingTarget; // Agent should face the target
    public bool rewardUseTimePenalty; // Hurry up

    [Header("Foot Grounded Visualization")] [Space(10)]
    public bool useFootGroundedVisualization;

    public MeshRenderer foot0;
    public MeshRenderer foot1;
    public MeshRenderer foot2;
    public MeshRenderer foot3;
    public Material groundedMaterial;
    public Material unGroundedMaterial;
    bool isNewDecisionStep;
    int currentDecisionStep;

    Quaternion lookRotation;
    Matrix4x4 targetDirMatrix;

    public override void InitializeAgent()
    {
        jdController = GetComponent<JointDriveController>();
        currentDecisionStep = 1;
        dirToTarget = target.position - body.position;


        //Setup each body part
        jdController.SetupBodyPart(body);
        jdController.SetupBodyPart(leg0Upper);
        jdController.SetupBodyPart(leg0Lower);
        jdController.SetupBodyPart(leg1Upper);
        jdController.SetupBodyPart(leg1Lower);
        jdController.SetupBodyPart(leg2Upper);
        jdController.SetupBodyPart(leg2Lower);
        jdController.SetupBodyPart(leg3Upper);
        jdController.SetupBodyPart(leg3Lower);
   }

    /// <summary>
    /// We only need to change the joint settings based on decision freq.
    /// </summary>
    public void IncrementDecisionTimer()
    {
        if (currentDecisionStep == agentParameters.numberOfActionsBetweenDecisions
            || agentParameters.numberOfActionsBetweenDecisions == 1)
        {
            currentDecisionStep = 1;
            isNewDecisionStep = true;
        }
        else
        {
            currentDecisionStep++;
            isNewDecisionStep = false;
        }
    }

    /// <summary>
    /// Add relevant information on each body part to observations.
    /// </summary>
    public void CollectObservationBodyPart(BodyPart bp)
    {
        var rb = bp.rb;
        AddVectorObs(bp.groundContact.touchingGround ? 1 : 0); // Whether the bp touching the ground

        Vector3 velocityRelativeToLookRotationToTarget = targetDirMatrix.inverse.MultiplyVector(rb.velocity);
        AddVectorObs(velocityRelativeToLookRotationToTarget);

        Vector3 angularVelocityRelativeToLookRotationToTarget = targetDirMatrix.inverse.MultiplyVector(rb.angularVelocity);
        AddVectorObs(angularVelocityRelativeToLookRotationToTarget);

        if (bp.rb.transform != body)
        {
            Vector3 localPosRelToBody = body.InverseTransformPoint(rb.position);
            AddVectorObs(localPosRelToBody);
            AddVectorObs(bp.currentXNormalizedRot); // Current x rot
            AddVectorObs(bp.currentYNormalizedRot); // Current y rot
            AddVectorObs(bp.currentZNormalizedRot); // Current z rot
            AddVectorObs(bp.currentStrength / jdController.maxJointForceLimit);
        }
    }

    public override void CollectObservations()
    {
        jdController.GetCurrentJointForces();

        // Update pos to target
        dirToTarget = target.position - body.position;
        lookRotation = Quaternion.LookRotation(dirToTarget);
        targetDirMatrix = Matrix4x4.TRS(Vector3.zero, lookRotation, Vector3.one);

        RaycastHit hit;
        if (Physics.Raycast(body.position, Vector3.down, out hit, 10.0f))
        {
            AddVectorObs(hit.distance);
        }
        else
            AddVectorObs(10.0f);

        // Forward & up to help with orientation
        Vector3 bodyForwardRelativeToLookRotationToTarget = targetDirMatrix.inverse.MultiplyVector(body.forward);
        AddVectorObs(bodyForwardRelativeToLookRotationToTarget);

        Vector3 bodyUpRelativeToLookRotationToTarget = targetDirMatrix.inverse.MultiplyVector(body.up);
        AddVectorObs(bodyUpRelativeToLookRotationToTarget);

        foreach (var bodyPart in jdController.bodyPartsDict.Values)
        {
            CollectObservationBodyPart(bodyPart);
        }
    }

    /// <summary>
    /// Agent touched the target
    /// </summary>
    public void TouchedTarget()
    {
        AddReward(1f);
        if (respawnTargetWhenTouched)
        {
            GetRandomTargetPos();
        }
    }

    // TODO
    // Obstacle penalty
    public void TouchedObstacle()
    {
        AddReward(-0.1f);
        // Reset Obstacle Position?
    }

    /// <summary>
    /// Moves target to a random position within specified radius.
    /// </summary>
    public void GetRandomTargetPos()
    {
        Vector3 newTargetPos = Random.insideUnitSphere * targetSpawnRadius;
        newTargetPos.y = 5;
        target.position = newTargetPos + ground.position;
    }

    public override void AgentAction(float[] vectorAction, string textAction)
    {
        if (detectTargets)
        {
            foreach (var bodyPart in jdController.bodyPartsDict.Values)
            {
                if (bodyPart.targetContact && !IsDone() && bodyPart.targetContact.touchingTarget)
                {
                    TouchedTarget();
                }
            }
        }

        // penalty for obstacles are enabled
        if (avoidObstacles)
        {
            foreach (var bodyPart in jdController.bodyPartsDict.Values)
            {
                if (bodyPart.obstacleContact && !IsDone() && bodyPart.targetContact.touchingTarget)
                {
                    TouchedObstacle();
                }
            }
        }

        // If enabled the feet will light up green when the foot is grounded.
        // This is just a visualization and isn't necessary for function
        if (useFootGroundedVisualization)
        {
            foot0.material = jdController.bodyPartsDict[leg0Lower].groundContact.touchingGround
                ? groundedMaterial
                : unGroundedMaterial;
            foot1.material = jdController.bodyPartsDict[leg1Lower].groundContact.touchingGround
                ? groundedMaterial
                : unGroundedMaterial;
            foot2.material = jdController.bodyPartsDict[leg2Lower].groundContact.touchingGround
                ? groundedMaterial
                : unGroundedMaterial;
            foot3.material = jdController.bodyPartsDict[leg3Lower].groundContact.touchingGround
                ? groundedMaterial
                : unGroundedMaterial;
        }

        // Joint update logic only needs to happen when a new decision is made
        if (isNewDecisionStep)
        {
            // The dictionary with all the body parts in it are in the jdController
            var bpDict = jdController.bodyPartsDict;

            int i = -1;
            // Pick a new target joint rotation
            bpDict[leg0Upper].SetJointTargetRotation(vectorAction[++i], vectorAction[++i], 0);
            bpDict[leg1Upper].SetJointTargetRotation(vectorAction[++i], vectorAction[++i], 0);
            bpDict[leg2Upper].SetJointTargetRotation(vectorAction[++i], vectorAction[++i], 0);
            bpDict[leg3Upper].SetJointTargetRotation(vectorAction[++i], vectorAction[++i], 0);
            bpDict[leg0Lower].SetJointTargetRotation(vectorAction[++i], 0, 0);
            bpDict[leg1Lower].SetJointTargetRotation(vectorAction[++i], 0, 0);
            bpDict[leg2Lower].SetJointTargetRotation(vectorAction[++i], 0, 0);
            bpDict[leg3Lower].SetJointTargetRotation(vectorAction[++i], 0, 0);

            // Update joint strength
            bpDict[leg0Upper].SetJointStrength(vectorAction[++i]);
            bpDict[leg1Upper].SetJointStrength(vectorAction[++i]);
            bpDict[leg2Upper].SetJointStrength(vectorAction[++i]);
            bpDict[leg3Upper].SetJointStrength(vectorAction[++i]);
            bpDict[leg0Lower].SetJointStrength(vectorAction[++i]);
            bpDict[leg1Lower].SetJointStrength(vectorAction[++i]);
            bpDict[leg2Lower].SetJointStrength(vectorAction[++i]);
            bpDict[leg3Lower].SetJointStrength(vectorAction[++i]);
        }

        // Set reward for this step according to mixture of the following elements.
        if (rewardMovingTowardsTarget)
        {
            RewardFunctionMovingTowards();
        }

        if (rewardFacingTarget)
        {
            RewardFunctionFacingTarget();
        }

        if (rewardUseTimePenalty)
        {
            RewardFunctionTimePenalty();
        }

        IncrementDecisionTimer();
    }

    /// Reward moving towards target & Penalize moving away from target.
    void RewardFunctionMovingTowards()
    {
        movingTowardsDot = Vector3.Dot(jdController.bodyPartsDict[body].rb.velocity, dirToTarget.normalized);
        AddReward(0.03f * movingTowardsDot);
    }

    /// Reward facing target & Penalize facing away from target
    void RewardFunctionFacingTarget()
    {
        facingDot = Vector3.Dot(dirToTarget.normalized, body.forward);
        AddReward(0.01f * facingDot);
    }

    /// <summary>
    /// Existential penalty for time-contrained tasks.
    /// </summary>
    void RewardFunctionTimePenalty()
    {
        AddReward(-0.001f);
    }

    /// <summary>
    /// Loop over body parts and reset them to initial conditions.
    /// </summary>
    public override void AgentReset()
    {
        if (dirToTarget != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(dirToTarget);
        }
        transform.Rotate(Vector3.up,Random.Range(0.0f, 360.0f));

        foreach (var bodyPart in jdController.bodyPartsDict.Values)
        {
            bodyPart.Reset(bodyPart);
        }

        isNewDecisionStep = true;
        currentDecisionStep = 1;
    }


    // TODO
    // get the closest target avaialble
}
