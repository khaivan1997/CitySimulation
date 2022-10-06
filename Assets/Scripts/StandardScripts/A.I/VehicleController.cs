using System.Collections.Generic;
using System;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Dynamics;
using VehicleNS;

[RequireComponent(typeof(Rigidbody))]
public class VehicleController : DynamicControllerBase
{
    public AxleInfo[] axleInfos;

#if UNITY_EDITOR
    [EditorReadOnly]
#endif
    public int subpathIndex;

    [HideInInspector]
    public float maxstopTorque;

#if UNITY_EDITOR
    [EditorReadOnly]
#endif
    public int numOfObstacles;
#if UNITY_EDITOR
    [EditorReadOnly]
#endif
    public bool isBraking;
#if UNITY_EDITOR
    [EditorReadOnly]
#endif
    public float avoidMultiplier;
#if UNITY_EDITOR
    [EditorReadOnly]
#endif
    public GameObject toAvoid;

    [HideInInspector]
    public Rigidbody rigidBody;
    //local avoidance
    protected Collider[] obstacles;

    protected override void Awake()
    {
        base.Awake();
        obstacles = new Collider[5];
        numOfObstacles = 0;
        rigidBody = GetComponent<Rigidbody>();

        EcsSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VehiclesSystem>();
        entity = ((VehiclesSystem)EcsSystem).Add(this); ;
    }

    public override void Init(int startIndex = 0, int endIndex = 0)
    {
        base.Init();
        this.pathCalculationJob = TrafficSystem.instance.calculatePath(transform.position, destination, EcsSystem.GetBuffer<VehiclePathBuffer>(entity));
    }

    public override void setDestination(float3 dest, int startIndex = 0, int endIndex = 0)
    {
        this.destination = dest;
        this.Init(startIndex, endIndex);
    }

    void Start()
    {
        DynamicControllerSystem.Add(this);
        foreach (AxleInfo axle in axleInfos)
            axle.init();
    }
    public bool move(Vector3 dest)
    {
        Vector3 velocity = GetVelocityVector(transform.position, dest);
        float steering;
        float motor = properties.speed;
        if (avoidMultiplier != 0)
        {
            steering = ((DynamicPropertiesVehicle)properties).maxSteeringAngle * avoidMultiplier;
            motor = motor / 2f;
        }
        else
        {
            Vector3 local = transform.InverseTransformPoint(dest);
            steering = Mathf.Abs(local.x / local.magnitude);
            steering = isObjectontheRight(transform.position, transform.right, dest) ? steering : -steering;
            steering = ((DynamicPropertiesVehicle)properties).maxSteeringAngle * steering;
        }


        //velocity.magnitude > agent.speed ? agent.speed : velocity.magnitude;
        //motor = Mathf.Abs(steering)>maxSteeringAngle/3 ? motor/1.5f :motor;// velocity.magnitude > maxMotorTorque ? maxMotorTorque : velocity.magnitude;
        //Debug.Log(" destination: "+ path.corners[subpathIndex]+" direction:" + velocity + " at:" + motor + " steerAngle: " + steering);
        float speed = moveCar(motor, steering);
        float decelerationForce = 0;
        if (steering > 25)
        {
            if (speed > 30)
                decelerationForce = maxstopTorque / 2f;
        }
        //if (velocity.magnitude <= properties.stopDistance && subpathIndex < pathResult.Length - 1)
        //{
        //    return false;
        //}

        deceleration(decelerationForce);
        return true;
    }


    public void stopMoving()
    {
        deceleration(float.MaxValue);
        this.rigidBody.isKinematic = true;
    }

    protected void deceleration(float stopTorgue)
    {
        foreach (AxleInfo axle in axleInfos)
        {
            if (axle.motor)
            {
                axle.leftWheelCollider.brakeTorque = stopTorgue;
                axle.rightWheelCollider.brakeTorque = stopTorgue;
            }
        }
    }


    public void ApplyLocalPositionToVisuals(AxleInfo axleInfo)
    {
        Vector3 position;
        Quaternion rotation;
        axleInfo.leftWheelCollider.GetWorldPose(out position, out rotation);
        Transform leftWheelMesh = axleInfo.leftWheel;
        leftWheelMesh.transform.position = position;
        leftWheelMesh.transform.rotation = axleInfo.leftMeshDefaultRotation * rotation;

        axleInfo.rightWheelCollider.GetWorldPose(out position, out rotation);
        Transform rightWheelMesh = axleInfo.rightWheel;
        rightWheelMesh.transform.position = position;
        rightWheelMesh.transform.rotation = axleInfo.rightMeshDefaultRotation * rotation;
    }

    public float moveCar(float motor, float steering)
    {
        float currentSpeed = 0;
        float angleTurnRate = Time.fixedDeltaTime * 30f;
        foreach (AxleInfo axle in axleInfos)
        {
            if (axle.steering)
            {
                float currentSteering = axle.leftWheelCollider.steerAngle;
                axle.leftWheelCollider.steerAngle = Mathf.Lerp(currentSteering, steering, angleTurnRate);
                axle.rightWheelCollider.steerAngle = Mathf.Lerp(currentSteering, steering, angleTurnRate);
            }
            if (axle.motor)
            {
                currentSpeed = 2 * Mathf.PI * axle.leftWheelCollider.radius * axle.leftWheelCollider.rpm * 60f / 3600f;
                if (currentSpeed < ((DynamicPropertiesVehicle)properties).maxSpeed)
                {
                    axle.leftWheelCollider.motorTorque = motor;
                    axle.rightWheelCollider.motorTorque = motor;
                }
            }
            ApplyLocalPositionToVisuals(axle);
        }
        return currentSpeed;
    }

    public override void Accept(DynamicVisitorBase v, int index)
    {
        v.VisitVehicle(this, index);
    }

    public override bool setForceStop(bool value)
    {
        forceStop = value;
        return forceStop;
    }

    public void FixedUpdateCall()
    {
        
    }

    public bool tryMoveToPosition(Vector3 dest)
    {
        if (havingUnavoidableObstacle(dest))
        {
            stopMoving();
            return true;
        }

        //force.y = 0;
        tryAvoidObstacle(dest);
        //Debug.Log("Velobefore:"+ force);
        //Debug.Log(gameObject.tag+" Veloafter:" + (destination - transform.position).normalized);
        rigidBody.isKinematic = false;
        if (!isBraking)
            return move(dest);
        return true;
    }

    public void tryAvoidObstacle(in Vector3 dest)
    {
        float maxDistance = properties.radius;
        numOfObstacles = Physics.OverlapSphereNonAlloc(transform.position, maxDistance, obstacles, properties.avoidableObstacles);
        Array.Sort(obstacles, 0, numOfObstacles, Comparer<Collider>.Create(
            (c1, c2) => (int)((c1.transform.position - transform.position).magnitude - (c2.transform.position - transform.position).magnitude)));
        bool isAvoid = false;
        avoidMultiplier = 0;
        Vector3 position = transform.position;
        Vector3 distanceVector = transform.forward;
        Vector3 right = transform.right;
        for (int i = 0; i < numOfObstacles; i++)
        {
            var collider = obstacles[i];
            if (collider.transform == this.transform)
                continue;
            float3 opponentPosition = collider.transform.position;
            if (isObjectinFront(position, distanceVector, opponentPosition))
            {
                Vector3 opponentForward = collider.transform.forward;
                toAvoid = collider.gameObject;
                if (MathUltilities.Ultilities_Burst.Angle2(distanceVector, opponentForward) < 90f)
                {
                    isBraking = true;
                    stopMoving();
                    return;
                }
                isAvoid = true;
                if (isObjectontheRight(position, right, opponentPosition))
                    avoidMultiplier -= 1f;
                else avoidMultiplier += 1f;
                break;
            }
        }
        if (!isAvoid)
            toAvoid = null;
        isBraking = false;
    }

    public bool havingUnavoidableObstacle(in Vector3 dest)
    {
        float maxDistance = properties.radius;
        Vector3 position = transform.position;
        numOfObstacles = Physics.OverlapSphereNonAlloc(position, properties.radius * 2, obstacles, properties.unavoidableObstacles);

        for (int i = 0; i < numOfObstacles; i++)
        {
            var collider = obstacles[i];
            if (isObjectinFront(position, GetVelocityVector(position, dest), collider.transform.position, 10f))
                return true;
        }
        return false;
    }


    public override void OnDisableCall()
    {
        base.OnDisableCall();
        EcsSystem.CommandBuffer.SetBuffer<VehiclePathBuffer>(entity);
    }

    public override void onDestinationReach()
    {
        EcsSystem.CommandBuffer.RemoveComponent<OnDestinationReach>(entity);
        if (haveJob)
            pathCalculationJob.Complete();
        EcsSystem.CommandBuffer.SetBuffer<VehiclePathBuffer>(entity);
        poolElement.release();
    }
}

[System.Serializable]
public class AxleInfo
{
    public WheelCollider leftWheelCollider;
    public Transform leftWheel;
    public WheelCollider rightWheelCollider;
    public Transform rightWheel;
    public bool motor;
    public bool steering;

    [HideInInspector]
    public Quaternion leftMeshDefaultRotation;
    [HideInInspector]
    public Quaternion rightMeshDefaultRotation;
    public void init()
    {
        leftMeshDefaultRotation = leftWheel.localRotation;
        rightMeshDefaultRotation = rightWheel.localRotation;
    }

}
