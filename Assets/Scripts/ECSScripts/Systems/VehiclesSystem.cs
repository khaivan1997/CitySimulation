using Unity.Collections;
using System.Collections.Generic;
using UnityEngine.Jobs;
using RaycastHit_Normal = UnityEngine.RaycastHit;
using RaycastHit_ECS = Unity.Physics.RaycastHit;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using Unity.Mathematics;
using Dynamics;
using VehicleNS;
using MathUltilities;

public class VehiclesSystem : MyDynamicSystemBase<VehicleController>
{
    NativeArray<float> _speeds;
    NativeArray<RaycastHit_Normal> _groundHits;
    NativeArray<RaycastCommand> _commandsGroundcheck;
    NativeList<int> _notMoveActive;
    float cooldown;
    float currentTime;

    public override Entity Add(VehicleController con)
    {
        Entity e = base.Add(con);
        int index = controllers.Count - 1;

#if UNITY_EDITOR
        EntityManager.SetName(e, $"vehicle_{index}");
#endif

        EntityManager.SetComponentData<VehicleTag>(e, new VehicleTag { index = index });
        DynamicPropertiesVehicle data = (DynamicPropertiesVehicle)con.properties;
        var mask_avoidableObstadcles = (uint)data.avoidableObstacles.value;
        var mask_unavoidableObstacles = (uint)data.unavoidableObstacles.value;
        EntityManager.SetComponentData<MovingData>(e, new MovingData
        {
            mask_avoidableObstacles = mask_avoidableObstadcles,
            mask_unavoidableObstacles = mask_unavoidableObstacles,
            radius = data.radius,
            height = data.height,
            speed = data.speed,
            stopDistance = data.stopDistance,
            max_steering_angle = data.maxSteeringAngle,
            max_speed = data.maxSpeed,
        });
        //Create collider
        var boxCol = con.transform.GetComponent<UnityEngine.BoxCollider>();
        var collider = Unity.Physics.BoxCollider.Create(new BoxGeometry
        {
            Center = boxCol.center,
            BevelRadius = boxCol.contactOffset,
            Orientation = Quaternion.identity,
            Size = boxCol.size,
        }, new CollisionFilter
        {
            BelongsTo = mask_avoidableObstadcles,
            CollidesWith = Constants.SpatialQuery_Dynamic_Layer,
            GroupIndex = 0
        });
        EntityManager.SetComponentData(e, new PhysicsCollider { Value = collider });
        EntityManager.SetComponentData<Rotation>(e, new Rotation { Value = Quaternion.identity });
        EntityManager.SetComponentData<Translation>(e, new Translation { Value = new float3(0, 0, 0) });
        EntityManager.AddBuffer<VehiclePathBuffer>(e);
        EntityManager.AddComponentData(e, PhysicsMass.CreateKinematic(MassProperties.UnitSphere));
        EntityManager.AddComponentData(e, new PhysicsGravityFactor { Value = 0 });
        return e;
    }

    protected override void AllocateMemory()
    {
        int length = controllers.Count;
        Debug.Log("allocate " + length);
        _speeds = new NativeArray<float>(length, Allocator.Persistent);
        //_heights = new NativeArray<float>(length, Allocator.Persistent);
        _groundHits = new NativeArray<RaycastHit_Normal>(length, Allocator.Persistent);
        _commandsGroundcheck = new NativeArray<RaycastCommand>(length, Allocator.Persistent);
        _notMoveActive = new NativeList<int>(length,Allocator.Persistent);
    }

    protected override void ClearMemory()
    {
        if (_speeds.IsCreated)
        {
            _speeds.Dispose();
            //_heights.Dispose();
            _groundHits.Dispose();
            _commandsGroundcheck.Dispose();
            _notMoveActive.Dispose();
        }
    }

    protected override void OnCreate()
    {
        baseArchetype = EntityManager.CreateArchetype(
           typeof(LocalToWorld),
           typeof(Rotation),
           typeof(Translation),
           typeof(VehicleTag),
           typeof(MovingData),
           typeof(PathProgression),
           typeof(PhysicsCollider),
           typeof(PhysicsVelocity),
           typeof(PhysicsExclude));

        int initialCapacity = 500;
        controllers = new List<VehicleController>(initialCapacity);
        transforms = new TransformAccessArray(initialCapacity);
        entities = new NativeList<Entity>(initialCapacity, Allocator.Persistent);
        postUpdateCommandBufferSystem = World.GetOrCreateSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
        _buildPhysicsWorld = World.DefaultGameObjectInjectionWorld.GetExistingSystem<BuildPhysicsWorld>();

        activeDynamicsQuery = GetEntityQuery(new EntityQueryDesc
        {
            None = new ComponentType[]
           {
               typeof(OnWaitingForPath), typeof(Inactive), typeof(OnDestinationReach)
           },
            All = new ComponentType[]{
               ComponentType.ReadOnly<VehicleTag>()
           }
        });

        this.cooldown = DynamicControllerSystem.stuckfixPeriod_vehicle;
        this.currentTime = this.cooldown;
    }

    protected override void MyBeforeUpdate()
    {
        Entities.WithoutBurst().WithAll<OnDestinationReach, VehicleTag>().WithNone<OnWaitingForPath>().ForEach((in VehicleTag tag) =>
        {
            controllers[tag.index].onDestinationReach();
        }).Run();

        Entities.WithoutBurst().WithAll<OnWaitingForPath, VehicleTag>().ForEach((in VehicleTag tag) =>
        {
            int index = tag.index;
            //Debug.Log($"got {index}");
            var controller = controllers[index];
            if (controller.haveJob && controller.pathCalculationJob.IsCompleted)
            {
                controller.getRowResult();
            }
        }).Run();
    }
    protected override void MyOnUpdate()
    {
        var ecb = postUpdateCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
        var speeds = _speeds;
        //var height = _heights;
        var collsionWorld = _buildPhysicsWorld.PhysicsWorld.CollisionWorld;
        var commandsGroundcheck = _commandsGroundcheck;
        var notMoveActive = _notMoveActive.AsParallelWriter();
        int groundMask = GameController.instance.dynamicsController.WalkingSurfaces;
        var deltaTime = this.Time.fixedDeltaTime;
        var intersections = GetComponentDataFromEntity<TrafficLightNS.CurrentLight>(true);
        uint intersection_layer = (uint)WalkingPath.WalkingSystem.instance.intersectionLayers.value;
        var pathBuffers = GetBufferFromEntity<VehiclePathBuffer>(true);

        short do_stuckfix = 0;
        currentTime -= deltaTime;
        if (currentTime < 0)
        {
            do_stuckfix = 1;
            currentTime = cooldown;
        }

        var canMoveJob = Entities.WithNone<OnWaitingForPath, Inactive, OnDestinationReach>().WithAll<VehicleTag>()
           .WithNativeDisableContainerSafetyRestriction(speeds)
           //.WithNativeDisableContainerSafetyRestriction(height)
           .WithNativeDisableContainerSafetyRestriction(commandsGroundcheck)
           .WithNativeDisableParallelForRestriction(notMoveActive)
           .WithReadOnly(collsionWorld)
           .WithReadOnly(pathBuffers)
           .WithReadOnly(intersections)
           .ForEach((Entity e, int entityInQueryIndex,
                   ref Translation translation, ref Rotation rotation, ref PathProgression progress,
                   in VehicleTag tag, in MovingData data) =>
           {
               #region Calculate next point
               float3 position = translation.Value;
               float radius = data.radius;
               var paths = pathBuffers[e];
               int length = paths.Length;
               var stopDistance = data.stopDistance;
               int pathIndex = progress.pathIndex;
               while (pathIndex < length && (DynamicControllerBase.GetVelocityVector(position, paths[pathIndex].position).magnitude() <= stopDistance))
                   pathIndex++;
               progress.pathIndex = pathIndex;
               int index = tag.index;
               #endregion
               //No more path
               if (pathIndex >= length)
               {
                   ecb.AddComponent<OnDestinationReach>(entityInQueryIndex, e);
                   progress.pathIndex = 0;
                   speeds[index] = 0;
                   commandsGroundcheck[index] = default;
               }
               else //Can move
               {
                   //On intersection
                   var node = paths[pathIndex];
                   float3 destination = node.position;
                   float speed = data.speed;
                   #region calculate speed profile
                   #endregion
                   //height[index] = data.height;
                   float3 dir = destination - position;

                   #region Handle Obstacle
                   float castDistance = stopDistance*4;
                   NativeList<ColliderCastHit> hits = new NativeList<ColliderCastHit>(3, Allocator.Temp);
                   var castDir = math.normalizesafe(dir);
                   var startPos = position + castDir * radius;
                   if (collsionWorld.SphereCastAll(startPos, data.radius, castDir, castDistance, ref hits, new CollisionFilter
                   {
                       BelongsTo = Constants.SpatialQuery_Vehicle_Layer,
                       CollidesWith = data.mask_avoidableObstacles | data.mask_unavoidableObstacles | (uint)intersection_layer,
                       GroupIndex = 0
                   }) && hits.Length >= 2)
                   {
                       var hit = hits[getClosetHit(hits, e, position)];
                       if (GetComponentDataFromEntity<TrafficLightNS.TrafficLightTag>(true).HasComponent(hit.Entity))
                       {
                           var intersection_road = GetComponentDataFromEntity<Parent>(true)[hit.Entity].Value;
                           if (intersections[intersection_road].Value == TrafficLight.LightIndex.RED)
                           {
                               if(math.dot( GetComponentDataFromEntity<LocalToWorld>(true)[intersection_road].Forward, dir) > 0)
                               {
                                   speeds[index] = 0;
                                   commandsGroundcheck[index] = default;
                                   return;
                               }
                           }
                       } else
                       {
                           speeds[index] = 0;
                           commandsGroundcheck[index] = default;
                           if(do_stuckfix > 0)
                                notMoveActive.AddNoResize(index);
                           return;
                       }
                   };
                   #endregion
                   hits.Dispose();

                   speeds[index] = speed;
                   #region Moving
                   float3 distanceVector = DynamicControllerBase.GetVelocityVector(position, destination);
                   float3 up = new float3(0, 1, 0);
                   Quaternion desiredRotation = Quaternion.LookRotation(distanceVector, up);

                   float3 desiredPosition = position + (math.normalizesafe(distanceVector)) * speed * deltaTime;
                   //Debug.Log($"at {index},{position} to {destination}, desired {desiredPosition}");
                   //position =  math.lerp(position, desiredPosition, 0.8f);                            
                   translation.Value = desiredPosition;
                   rotation.Value = Quaternion.LookRotation(desiredPosition - position, up);
                   desiredPosition.y += heightDistance;
                   commandsGroundcheck[index] = new RaycastCommand(desiredPosition, -up, maxGroundCheckDistance, groundMask);

                   #endregion
               }
           }).WithBurst(Unity.Burst.FloatMode.Default, Unity.Burst.FloatPrecision.Standard, true)
          .WithName("Update_path_for_vehicles")
          .ScheduleParallel(this.Dependency);

        postUpdateCommandBufferSystem.AddJobHandleForProducer(canMoveJob);

        var groundCheckJob = RaycastCommand.ScheduleBatch(_commandsGroundcheck, _groundHits, 1, canMoveJob);

        dynamicJob = new VehicleOnGroundJob
        {
            translations = GetComponentDataFromEntity<Translation>(),
            rotations = GetComponentDataFromEntity<Rotation>(),
            entities = entities,
            speeds = _speeds,
            resultGround = _groundHits,
            movingData = GetComponentDataFromEntity<MovingData>(true),
        }.Schedule(transforms, groundCheckJob);

        DynamicControllerSystem.instance.numActiveVehicles_Text.text = activeDynamicsQuery.CalculateEntityCount() + " active vehicles";
        dynamicJob.Complete();

        //Debug.Log($"got {cooldown}, {currentTime}");
        //stuck fix
        if (do_stuckfix > 0)
        {
            int length = this._notMoveActive.Length;
            for (int i = 0; i < length; i++)
            {
                //Debug.Log($"got fix {this._notMoveActive[i]}");
                this.controllers[this._notMoveActive[i]].fix.FixStuck();
            }
            this._notMoveActive.Clear();
        }
    }
}
