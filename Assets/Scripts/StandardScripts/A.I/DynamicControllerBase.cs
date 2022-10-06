using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Dynamics;

[RequireComponent(typeof(PoolElement))]
public abstract class DynamicControllerBase : MonoBehaviour, DynamicElementBase
{
    public DynamicProperties properties;
    public float3 destination;
    public Entity entity;
    protected MySystemBaseWithCommandBuffer EcsSystem;

    [Header("info")]
#if UNITY_EDITOR
    [EditorReadOnly]
#endif
    public bool haveJob = false;

    [SerializeField]
#if UNITY_EDITOR
    [EditorReadOnly]
#endif
    protected bool _forceStop;
    public bool forceStop
    {
        get => _forceStop;
        protected set
        {
            _forceStop = value;
        }
    }

    public JobHandle pathCalculationJob;

    //DebugOnly
    //public float3[] nodes;

    [HideInInspector]
    public PoolElement poolElement;
    [HideInInspector]
    public HardStuckFix fix;

    protected virtual void Awake()
    { 
        poolElement = GetComponent<PoolElement>();
        fix = GetComponent<HardStuckFix>();
    }

   
    //ConstrantInterfaces

    public abstract void setDestination(float3 dest, int startIndex = 0, int endIndex = 0);
    public void getRowResult()
    {
        this.pathCalculationJob.Complete();
        this.haveJob = false;
        EcsSystem.CommandBuffer.RemoveComponent<OnWaitingForPath>(entity);
        EcsSystem.CommandBuffer.RemoveComponent<Unity.Physics.PhysicsExclude>(entity);
    }

    public virtual void Init(int startIndex = 0, int endIndex = 0)
    {
        this.haveJob = true;
        EcsSystem.CommandBuffer.AddComponent<OnWaitingForPath>(entity);
        EcsSystem.CommandBuffer.RemoveComponent<Inactive>(entity);
        EcsSystem.CommandBuffer.SetComponent<Translation>(entity, new Translation { Value = transform.position });
    }

    //public abstract bool move(Vector3 dest);
    //public abstract void stopMoving();


    public abstract void onDestinationReach();
    public virtual void OnDisableCall()
    {
        EcsSystem.CommandBuffer.AddComponent<Inactive>(entity);
        EcsSystem.CommandBuffer.AddComponent<Unity.Physics.PhysicsExclude>(entity);
        EcsSystem.CommandBuffer.SetComponent<Translation>(entity, new Translation { Value = new float3(0, 0, 0) });
        if (haveJob)
        {
            pathCalculationJob.Complete();
            haveJob = false;
        }
        transform.position = Vector3.zero;
    }

    ///<summary>
    ///try set forceStop to value, return forceStop value after set
    ///</summary>
    public abstract bool setForceStop(bool value);

    public abstract void Accept(DynamicVisitorBase v, int index);

    public static bool isObjectinFront(in float3 position, in float3 forward, in float3 other, float viewPort = 30f)
    {
        Vector3 distanceVector = other - position;
        float result = MathUltilities.Ultilities_Burst.Angle2(forward, distanceVector);
        return result <= viewPort;
    }
    public static bool isObjectontheRight(in float3 position, in float3 right, in float3 other)
    {
        Vector3 distanceVector = other - position;
        //Debug.Log("RightVector:" + transform.right + " " + other + " and relative:"+ (transform.InverseTransformPoint(other).x > 0));
        float result = MathUltilities.Ultilities_Burst.Angle2(right, distanceVector);
        return result < 90f;

    }

    public static float3 GetVelocityVector(in float3 position, in float3 des, bool discardY = true)
    {
        Vector3 velocity = des - position;
        if (discardY)
            velocity.y = 0;
        return velocity;
    }
}
