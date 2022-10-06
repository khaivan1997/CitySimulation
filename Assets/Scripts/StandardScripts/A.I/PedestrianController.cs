using UnityEngine;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Entities;
using WalkingPath;
using Dynamics;

public class PedestrianController : DynamicControllerBase
{
    //animator
    Animator animator;
    int animatorID_isWalking;


    protected override void Awake()
    {
        base.Awake();
        animator = GetComponent<Animator>();
        animatorID_isWalking = Animator.StringToHash("isWalking");

        EcsSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<PedestrianUpdateSystem>();
        entity = ((PedestrianUpdateSystem)EcsSystem).Add(this);
    }


    public override void Init(int startIndex = 0, int endIndex = 0)
    {
        base.Init(startIndex, endIndex);
        if (startIndex != 0 && endIndex != 0)
        {
            this.pathCalculationJob = WalkingSystem.instance.calculatePath(startIndex, endIndex,
                EcsSystem.GetBuffer<WalkingPathBuffer>(entity));
        }
        else
        {
            this.pathCalculationJob = WalkingSystem.instance.calculatePath(transform.position, destination,
                  EcsSystem.GetBuffer<WalkingPathBuffer>(entity));
        }
    }

    public override void setDestination(float3 dest, int startIndex = 0, int endIndex = 0)
    {
        this.destination = dest;
        this.Init(startIndex, endIndex);
    }

    public override void OnDisableCall()
    {
        base.OnDisableCall();
        EcsSystem.CommandBuffer.SetBuffer<WalkingPathBuffer>(entity);
    }

    public override void onDestinationReach()
    {
        EcsSystem.CommandBuffer.RemoveComponent<OnDestinationReach>(entity);
        if (haveJob)
            pathCalculationJob.Complete();
        EcsSystem.CommandBuffer.SetBuffer<WalkingPathBuffer>(entity);
        poolElement.release();
    }

    public override void Accept(DynamicVisitorBase v, int index)
    {
        v.VisitPedestrian(this, index);
    }

    bool isWalking;
    public void SetWalking(bool value)
    {
        if(isWalking != value)
        {
            isWalking = value;
            animator.SetBool(animatorID_isWalking, value);
        }   
    }

    public override bool setForceStop(bool value)
    {
        //if (value == false)
        //{
        //    forceStop = false;
        //    return forceStop;
        //}
        //else
        //{
        //    if (!haveJob && subpathIndex < pathNodes.Length)
        //    {
        //        if (subpathIndex > 0)
        //        {
        //            MyGraphNode previous_node = WalkingSystem.instance.graphNodes[pathNodes[subpathIndex - 1]];
        //            if ((previous_node.mapArea == MapArea.ROAD || previous_node.mapArea == MapArea.ROAD_INTERSECTION) &&
        //            !WalkingSystem_Ultilities_Burst.isPointInsideTriangleXZ(previous_node.vertex0, previous_node.vertex1, previous_node.vertex2, transform.position))
        //            {
        //                forceStop = false;
        //                return forceStop;
        //            }
        //        }
        //        MyGraphNode target_node = WalkingSystem.instance.graphNodes[pathNodes[subpathIndex]];
        //        if ((target_node.mapArea == MapArea.ROAD || target_node.mapArea == MapArea.ROAD_INTERSECTION) &&
        //            !WalkingSystem_Ultilities_Burst.isPointInsideTriangleXZ(target_node.vertex0, target_node.vertex1, target_node.vertex2, transform.position))
        //        {
        //            forceStop = false;
        //            return forceStop;
        //        }
        //    }
        //    forceStop = true;
        //    return forceStop;
        //}
        return true;
    }

}
