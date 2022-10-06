using Unity.Collections;
using System.Collections.Generic;
using UnityEngine.Jobs;
using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Jobs;
using Unity.Physics;
using Unity.Mathematics;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(EndFramePhysicsSystem))]
[AlwaysSynchronizeSystem]
public abstract class MyDynamicSystemBase<T> : MySystemBaseWithCommandBuffer where T:DynamicControllerBase
{
    //const params ground check
    public static readonly float heightDistance = 10;
    public static readonly float maxGroundCheckDistance = 30f;
    //end update command buffer
    protected EndFixedStepSimulationEntityCommandBufferSystem postUpdateCommandBufferSystem;
    // active query for count
    protected EntityQuery activeDynamicsQuery;
    //physics world
    protected BuildPhysicsWorld _buildPhysicsWorld;
    //should be define in onCreate
    protected EntityArchetype baseArchetype;
    //JobHandle
    protected JobHandle dynamicJob;
    //internal variable for setup memory
    private bool dynamicChange = false;

    //dynamics data
    public List<T> controllers;
    public TransformAccessArray transforms;
    public NativeList<Entity> entities;

    public virtual Entity Add(T con)
    {
        controllers.Add(con);
        transforms.Add(con.transform);
        Entity e = EntityManager.CreateEntity(baseArchetype);
        entities.Add(e);
        dynamicChange = true;
        return e;
    }

    protected sealed override void OnUpdate()
    {
        #region Setup for Jobs
        if (dynamicChange)
        {
            ClearMemory();
            AllocateMemory();
            dynamicChange = false;
        }
        MyBeforeUpdate();

        PlaybackAndClearCommandBuffer();
        #endregion

        MyOnUpdate();
    }
    protected abstract void MyBeforeUpdate();
    protected abstract void MyOnUpdate();
    protected abstract void AllocateMemory();
    protected abstract void ClearMemory();

    public static int getClosetHit(NativeList<RaycastHit> hits, Entity currentEntity, float3 position)
    {
        int closet = -1;
        float pos = 0f;
        for(int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if(hit.Entity != currentEntity)
            {
                var dist = math.distance(hit.Position, position);
                if (closet == -1 || pos > dist )
                {
                    closet = i;
                    pos = dist;
                }
            }   
        }
        return closet;
    }

    public static int getClosetHit(NativeList<ColliderCastHit> hits, Entity currentEntity, float3 position)
    {
        int closet = -1;
        float pos = 0f;
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.Entity != currentEntity)
            {
                var dist = math.distance(hit.Position, position);
                if (closet == -1 || pos > dist)
                {
                    closet = i;
                    pos = dist;
                }
            }
        }
        return closet;
    }
    protected sealed override void OnDestroy()
    {
        base.OnDestroy();
        entities.Dispose();
        transforms.Dispose();
        ClearMemory();
    }
}

[AlwaysUpdateSystem]
[AlwaysSynchronizeSystem]
public abstract class MySystemBaseWithCommandBuffer : SystemBase
{
    //command buffer
    protected EntityCommandBuffer? commandBuffer;
    public EntityCommandBuffer CommandBuffer
    {
        get
        {
            if (commandBuffer == null)
                commandBuffer = new EntityCommandBuffer(Allocator.TempJob, PlaybackPolicy.SinglePlayback);
            return commandBuffer.Value;
        }
    }
    protected void PlaybackAndClearCommandBuffer()
    {
        if (commandBuffer != null)
        {
            commandBuffer.Value.Playback(this.EntityManager);
            commandBuffer.Value.Dispose();
            commandBuffer = null;
        }
    }
}

public static class Constants
{
    public static readonly uint SpatialQuery_Pedestrian_Layer = 1u;
    public static readonly uint SpatialQuery_Vehicle_Layer = 2u;
    public static readonly uint SpatialQuery_Dynamic_Layer = SpatialQuery_Vehicle_Layer | SpatialQuery_Pedestrian_Layer;
}