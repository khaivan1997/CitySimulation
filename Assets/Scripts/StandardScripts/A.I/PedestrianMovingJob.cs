using UnityEngine.Jobs;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

//[BurstCompile(CompileSynchronously = true)]
//public struct PedestrianMovingJob : IJobParallelForTransform
//{
//    [ReadOnly]
//    [DeallocateOnJobCompletion]
//    public NativeArray<float3> destinations;
//    [ReadOnly]
//    [DeallocateOnJobCompletion]
//    public NativeArray<RaycastHit> resultAvoidable;
//    [ReadOnly]
//    [DeallocateOnJobCompletion]
//    public NativeArray<RaycastHit> resultUnavoidable;
//    [ReadOnly]
//    [DeallocateOnJobCompletion]
//    public NativeArray<float> speeds;
//    [ReadOnly]
//    public int groundMask;
//    [ReadOnly]
//    public float timeStep;
//    [WriteOnly]
//    public NativeArray<RaycastCommand> commandsGroundcheck;
//    [NativeDisableParallelForRestriction]
//    public NativeBitArray canMove;
//    public void Execute(int index, TransformAccess transform)
//    {
//        if (canMove.IsSet(index))
//        {
//            float3 position = transform.position;
//            float3 destination = destinations[index];
//            float3 distanceVector = DynamicControllerBase.GetVelocityVector(position, destination);
//            float3 up = new float3(0, 1, 0);
//            Quaternion desiredRotation = Quaternion.LookRotation(distanceVector, up);
//            float3 right = desiredRotation * new float3(1, 0, 0);

//            if (RayCastHitConvert.GetColliderID(resultUnavoidable[index]) != 0)
//                canMove.Set(index, false);
//            else
//            {
//                float avoidMultiplier = 0f;
//                if (RayCastHitConvert.GetColliderID(resultAvoidable[index]) != 0)
//                {
//                    if (DynamicControllerBase.isObjectontheRight(position, right, resultAvoidable[index].point))
//                    {
//                        avoidMultiplier = -.5f;
//                    }
//                    else
//                        avoidMultiplier = .5f;
//                }

//                float3 desiredPosition = position + (math.normalizesafe(distanceVector) + right * avoidMultiplier) * speeds[index] * timeStep;
//                //Debug.Log($"at {index},{position}to{destinations[index]},{avoidMultiplier}, desired{desiredPosition}");
//                //position =  math.lerp(position, desiredPosition, 0.8f);                            
//                transform.position = desiredPosition;
//                transform.rotation = Quaternion.LookRotation(desiredPosition - position, up);
//                position.y = destination.y + 10f;
//                commandsGroundcheck[index] = new RaycastCommand(position, -up, float.MaxValue, groundMask);
//            }
//        }
//    }
//}

//[BurstCompile(CompileSynchronously = true)]
//public struct PedestrianOnGroundJob : IJobParallelForTransform
//{
//    [ReadOnly]
//    public NativeBitArray canMove;
//    [ReadOnly]
//    [DeallocateOnJobCompletion]
//    public NativeArray<RaycastHit> resultGround;
//    [ReadOnly]
//    [DeallocateOnJobCompletion]
//    public NativeArray<float> heights;

//    public void Execute(int index, TransformAccess transform)
//    {
//        if (canMove.IsSet(index))
//        {
//            float height = heights[index];
//            float3 position = transform.position;
//            float3 groundPoint = resultGround[index].point;
//            position.y = groundPoint.y + height / 2f;
//            transform.position = position;
//        }
//    }
//}
