using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Transforms;
using Unity.Physics.Systems;
using Unity.Physics;
using UnityEngine;
using RaycastHit_Normal = UnityEngine.RaycastHit;
using RaycastHit_ECS = Unity.Physics.RaycastHit;
using GPUInstancer;
using MathUltilities;
using Dynamics;
using WalkingPath;

public class PedestrianUpdateSystem : MyDynamicSystemBase<PedestrianController>
{
    NativeArray<float> _speeds;
    NativeArray<RaycastHit_Normal> _groundHits;
    NativeArray<RaycastCommand> _commandsGroundcheck;
    NativeArray<float4x4> _localToWorldTransforms;

    ComputeBuffer transformBuffer;

    public override Entity Add(PedestrianController con)
    {
        Entity e = base.Add(con);
        int index = controllers.Count - 1;

#if UNITY_EDITOR
        EntityManager.SetName(e, $"pedestrian_{index}");
#endif

        EntityManager.AddSharedComponentData<PedestrianType>(e, new PedestrianType { category = PedestrianCategory.MOVING });
        EntityManager.SetComponentData<PedestrianTag>(e, new PedestrianTag { index = index });
        var data = con.properties;
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
        });
        //Create collider
        var collider = Unity.Physics.CapsuleCollider.Create(new CapsuleGeometry
        {
            Radius = data.radius,
            Vertex0 = new float3(0, 0, 0),
            Vertex1 = new float3(0, data.height, 0),
        }, new CollisionFilter
        {
            BelongsTo = mask_avoidableObstadcles,
            CollidesWith = Constants.SpatialQuery_Dynamic_Layer,
            GroupIndex = 0
        });
        EntityManager.SetComponentData(e, new PhysicsCollider { Value = collider });
        EntityManager.SetComponentData<Rotation>(e, new Rotation { Value = Quaternion.identity });
        EntityManager.SetComponentData<Translation>(e, new Translation { Value = new float3(0, 0, 0) });
        EntityManager.AddBuffer<WalkingPathBuffer>(e);
        EntityManager.AddComponentData(e, PhysicsMass.CreateKinematic(MassProperties.UnitSphere));
        EntityManager.AddComponentData(e, new PhysicsGravityFactor { Value = 0 });
        //EntityManager.AddComponentData(e, new PhysicsDebugDisplayData
        //{
        //    DrawBroadphase = 0,
        //    DrawColliders = 1,
        //    DrawColliderAabbs = 0,
        //    DrawColliderEdges = 1
        //});
        return e;
    }

    protected override void OnCreate()
    {
        baseArchetype = EntityManager.CreateArchetype(
           typeof(LocalToWorld),
           typeof(Rotation),
           typeof(Translation),
           typeof(PedestrianTag),
           typeof(MovingData),
           typeof(PathProgression),
           typeof(PhysicsCollider),
           typeof(PhysicsVelocity),
           typeof(PhysicsExclude));

        int initialCapacity = 5000;
        controllers = new List<PedestrianController>(initialCapacity);
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
               ComponentType.ReadOnly<PedestrianTag>()
           }
        });
    }


    protected override void AllocateMemory()
    {
        int length = controllers.Count;
        _speeds = new NativeArray<float>(length, Allocator.Persistent);
        //_heights = new NativeArray<float>(length, Allocator.Persistent);
        _groundHits = new NativeArray<RaycastHit_Normal>(length, Allocator.Persistent);
        _commandsGroundcheck = new NativeArray<RaycastCommand>(length, Allocator.Persistent);
        _localToWorldTransforms = new NativeArray<float4x4>(length, Allocator.Persistent);

        List<GPUInstancerPrototype> x = GPUInstancerAPI.GetPrototypeList(GameController.instance.crowdManager);
        transformBuffer = GPUInstancerAPI.GetTransformDataBuffer(GameController.instance.crowdManager, x[0]);
    }

    protected override void ClearMemory()
    {
        if (_speeds.IsCreated)
        {
            _speeds.Dispose();
            _groundHits.Dispose();
            _commandsGroundcheck.Dispose();
            _localToWorldTransforms.Dispose();
        }
    }

    protected override void MyBeforeUpdate()
    {
        Entities.WithoutBurst().WithAll<OnDestinationReach, PedestrianTag>().WithNone<OnWaitingForPath>().ForEach((in PedestrianTag tag) =>
            {
                controllers[tag.index].onDestinationReach();
            }).Run();

        Entities.WithoutBurst().WithAll<OnWaitingForPath, PedestrianTag>().ForEach((in PedestrianTag tag) =>
        {
            var controller = controllers[tag.index];
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
        var graphNodes = WalkingSystem.instance.graphNodes;
        var collsionWorld = _buildPhysicsWorld.PhysicsWorld.CollisionWorld;
        var commandsGroundcheck = _commandsGroundcheck;
        int groundMask = GameController.instance.dynamicsController.WalkingSurfaces;
        var deltaTime = this.Time.fixedDeltaTime;
        var intersections = GetComponentDataFromEntity<TrafficLightNS.CurrentLight>(true);
        uint intersection_layer = (uint)WalkingSystem.instance.intersectionLayers.value;
        var pathBuffers = GetBufferFromEntity<WalkingPathBuffer>(true);
        //Entities.WithoutBurst().WithNone<Inactive>().WithAll<PedestrianTag>().WithReadOnly(speeds).ForEach((in PedestrianTag tag) =>
        //{
        //    int index = tag.index;
        //    var pedestrian = pedestrians[index];
        //    if (speeds[index] > 0)
        //        pedestrians[index].SetWalking(true);
        //    else pedestrians[index].SetWalking(false);
        //    count++;
        //}).Run();

        var canMoveJob = Entities.WithNone<OnWaitingForPath, Inactive, OnDestinationReach>().WithAll<PedestrianTag>()
            .WithNativeDisableContainerSafetyRestriction(speeds)
            //.WithNativeDisableContainerSafetyRestriction(height)
            .WithNativeDisableContainerSafetyRestriction(commandsGroundcheck)
            .WithReadOnly(graphNodes)
            .WithReadOnly(collsionWorld)
            .WithNativeDisableParallelForRestriction(pathBuffers)
            .WithReadOnly(intersections)
            .ForEach((Entity e, int entityInQueryIndex,
                    ref Translation translation, ref Rotation rotation, ref PathProgression progress,
                    in PedestrianTag tag, in MovingData data) =>
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
                  var graphNode = graphNodes[node.node_index];
                  if ( graphNode.mapArea == MapArea.ROAD_INTERSECTION 
                  && !Ultilities_Burst.isPointInsideTriangleXZ(graphNode.vertex0, graphNode.vertex1, graphNode.vertex2, position)
                   && (pathIndex == 0 || (pathIndex > 0 &&
                       ( graphNodes[paths[pathIndex - 1].node_index].mapArea == MapArea.ROAD_SIDEWALK 
                       || graphNodes[paths[pathIndex - 1].node_index].mapArea == MapArea.ROAD_SIDEWALK_INTERSECTION))))
                  {
                      RaycastHit_ECS intersection_hit = new RaycastHit_ECS();
                      var startPos = translation.Value;
                      var h = 5f;
                      startPos.y += h;
                      var dist = graphNode.getCenter() - startPos;
                      var endPos = startPos + 2*dist;
                      if (collsionWorld.CastRay(new RaycastInput
                      {
                          Start = translation.Value,
                          End = endPos,
                          Filter = new CollisionFilter
                          {
                              BelongsTo = Constants.SpatialQuery_Pedestrian_Layer,
                              CollidesWith = intersection_layer,
                              GroupIndex = 0,
                          }
                      }, out intersection_hit))
                      {
                          if (intersections[intersection_hit.Entity].Value == TrafficLight.LightIndex.GREEN )
                          {
                              speeds[index] = 0;
                              commandsGroundcheck[index] = default;
                              return;
                          }
                      }
                  }
                  float3 destination = node.position;
			      float speed = data.speed;	
			      #region calculate speed profile
				  #endregion
                  //height[index] = data.height;
                  float3 dir = destination - position;

                  #region Handle Obstacle
                  float castDistance = stopDistance * 10;
                  NativeList<RaycastHit_ECS> hits_Avoidable = new NativeList<RaycastHit_ECS>(2, Allocator.Temp);
                  RaycastHit_ECS hitAvoidable = new RaycastHit_ECS();
                  if (!collsionWorld.CastRay(new RaycastInput
                  {
                      Start = position,
                      End = position + math.normalize(dir) * castDistance,
                      Filter = new CollisionFilter
                      {
                          BelongsTo = Constants.SpatialQuery_Pedestrian_Layer,
                          CollidesWith = data.mask_avoidableObstacles,
                          GroupIndex = 0
                      }
                  }, ref hits_Avoidable) || hits_Avoidable.Length <= 1)
                  {
                      hitAvoidable.RigidBodyIndex = -1;
                  } else hitAvoidable = hits_Avoidable[getClosetHit(hits_Avoidable, e, position)];
                  hits_Avoidable.Dispose();
                  //commandsUnavoidable[index] = new SpherecastCommand(position, radius, dir, castDistance * 2, data.mask_unavoidableObstacles);
                  #endregion

                  #region Moving
                  float3 distanceVector = DynamicControllerBase.GetVelocityVector(position, destination);
                  float3 up = new float3(0, 1, 0);
                  Quaternion desiredRotation = Quaternion.LookRotation(distanceVector, up);
                  float3 right = desiredRotation * new float3(1, 0, 0);
                  //if (resultUnavoidable[index].RigidBodyIndex > 0)
                  //{
                  //    commandsGroundcheck[index] = default;
                  //    speeds[index] = 0;
                  //}
                  //else
                  {
                      speeds[index] = speed;
                      float avoidMultiplier = 0f;
                      if (hitAvoidable.RigidBodyIndex > 0)
                      {
                          if (DynamicControllerBase.isObjectontheRight(position, right, hitAvoidable.Position))
                          {
                              avoidMultiplier = -.5f;
                          }
                          else
                              avoidMultiplier = .5f;
                      }

                      float3 desiredPosition = position + (math.normalizesafe(distanceVector) + right * avoidMultiplier) * speed * deltaTime;
                      //Debug.Log($"at {index},{position}to{destinations[index]},{avoidMultiplier}, desired{desiredPosition}");
                      //position =  math.lerp(position, desiredPosition, 0.8f);                            
                      translation.Value = desiredPosition;
                      rotation.Value = Quaternion.LookRotation(desiredPosition - position, up);
                      desiredPosition.y += heightDistance;
                      commandsGroundcheck[index] = new RaycastCommand(desiredPosition, -up, maxGroundCheckDistance, groundMask);
                  }
                  #endregion
              }

          }).WithBurst(Unity.Burst.FloatMode.Default, Unity.Burst.FloatPrecision.Standard, true)
          .WithName("Update_path_for_pedestrians")
          .ScheduleParallel(this.Dependency);

        postUpdateCommandBufferSystem.AddJobHandleForProducer(canMoveJob);

        var groundCheckJob = RaycastCommand.ScheduleBatch(_commandsGroundcheck, _groundHits, 1, canMoveJob);

        dynamicJob = new PedestrianOnGroundJob
        {
            translations = GetComponentDataFromEntity<Translation>(),
            rotations = GetComponentDataFromEntity<Rotation>(true),
            entities = entities,
            speeds = _speeds,
            resultGround = _groundHits,
            localToWorldMatrix = _localToWorldTransforms,
        }.Schedule(transforms, groundCheckJob);

        //var time = UnityEngine.Time.realtimeSinceStartup;
        //int length = speeds.Length;
        //for (int i = 0; i < length; i++)
        //{
        //    if (speeds[i] > 0)
        //    {
        //       // pedestrians[i].SetWalking(true);
        //        count++;
        //    }

        //    //else pedestrians[i].SetWalking(false);
        //}

        //GetComponentDataFromEntity<Translation>();
        DynamicControllerSystem.instance.numActivePedestrians_Text.text = activeDynamicsQuery.CalculateEntityCount() + " active pedestrians";
        //Debug.Log($"accumulate:  {UnityEngine.Time.realtimeSinceStartup - time}s");

        dynamicJob.Complete();
        transformBuffer.SetData(_localToWorldTransforms);
    }
}
