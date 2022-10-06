using Unity.Entities;
using Unity.Mathematics;
using TrafficLightNS;
using Unity.Physics;
using Unity.Transforms;
using Unity.Collections;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PedestrianUpdateSystem))]
[UpdateBefore(typeof(VehiclesSystem))]
public class TrafficLightsSystem : MySystemBaseWithCommandBuffer
{
    EntityArchetype trafficLightArchetype;
    EntityArchetype childArchertype;
    public Entity createEntity(IntersectionRoad r)
    {
        var e = EntityManager.CreateEntity(this.trafficLightArchetype);

#if UNITY_EDITOR
        EntityManager.SetName(e, r.GetComponentInParent<IntersectionManager>().name + "_" + r.name);
#endif

        EntityManager.SetComponentData<CurrentLight>(e, new CurrentLight { Value = r.getCurrentLight() });
        EntityManager.SetComponentData<LocalToWorld>(e, new LocalToWorld { Value = r.transform.localToWorldMatrix });
        //Create collider
        var boxCol = r.GetComponent<UnityEngine.BoxCollider>();
        var pedestrian_filter = new CollisionFilter
        {
            BelongsTo = (uint)WalkingPath.WalkingSystem.instance.intersectionLayers.value,
            CollidesWith = Constants.SpatialQuery_Pedestrian_Layer,
            GroupIndex = 0,
        };
        var vehicle_filter = new CollisionFilter
        {
            BelongsTo = (uint)WalkingPath.WalkingSystem.instance.intersectionLayers.value,
            CollidesWith = Constants.SpatialQuery_Vehicle_Layer,
            GroupIndex = 0,
        };
        var collider = Unity.Physics.BoxCollider.Create(new BoxGeometry
        {
            Center = boxCol.center,
            BevelRadius = boxCol.contactOffset,
            Orientation = quaternion.identity,
            Size = boxCol.size,
        }, pedestrian_filter);
        EntityManager.SetComponentData<PhysicsCollider>(e, new PhysicsCollider { Value = collider });

        //child component for car
        var carCol = r.GetComponentInChildren<CarTriggerCollider>();
        var mesh = carCol.GetComponent<UnityEngine.MeshFilter>().sharedMesh;
        float4x4 scaleMatrix = float4x4.Scale(carCol.transform.lossyScale);
        var vertices = new NativeArray<float3>(mesh.vertices.Length, Allocator.Temp);
        for (int i = 0; i < mesh.vertices.Length; i++)
        {
            float3 vertice = mesh.vertices[i];
            vertices[i] = math.mul(scaleMatrix, new float4(vertice.x, vertice.y, vertice.z, 1)).xyz;
        }
        int length = mesh.triangles.Length;
        var triangles = new NativeArray<int3>(length / 3, Allocator.Temp);
        for (int i = 0; i < length; i += 3)
        {
            triangles[i / 3] = new int3
            {
                x = mesh.triangles[i],
                y = mesh.triangles[i + 1],
                z = mesh.triangles[i + 2]
            };
        }
        collider = Unity.Physics.MeshCollider.Create(
            vertices, triangles, vehicle_filter);
        var childEntity = EntityManager.CreateEntity(childArchertype);
        EntityManager.SetComponentData<LocalToWorld>(childEntity, new LocalToWorld { Value = carCol.transform.localToWorldMatrix });
        EntityManager.SetComponentData<LocalToParent>(childEntity, new LocalToParent
        { Value = float4x4.TRS(carCol.transform.localPosition, carCol.transform.localRotation, carCol.transform.localScale) });
        EntityManager.SetComponentData<PhysicsCollider>(childEntity, new PhysicsCollider { Value = collider });
        EntityManager.SetComponentData<Parent>(childEntity, new Parent { Value = e });
        vertices.Dispose();
        triangles.Dispose();

        return e;
    }

    protected override void OnCreate()
    {
        trafficLightArchetype = EntityManager.CreateArchetype(
            typeof(TrafficLightTag),
            typeof(LocalToWorld),
            typeof(CurrentLight),
            typeof(PhysicsCollider)
           );
        childArchertype = EntityManager.CreateArchetype(
            typeof(TrafficLightTag),
            typeof(LocalToWorld),
            typeof(LocalToParent),
            typeof(Parent),
           //typeof(CompositeScale),
            typeof(PhysicsCollider)
           );
    }

    protected override void OnUpdate()
    {
        this.PlaybackAndClearCommandBuffer();
    }

}

