using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Transforms;
using Unity.Entities;
using RaycastHit_Normal = UnityEngine.RaycastHit;
using Dynamics;

namespace VehicleNS
{
    [BurstCompile(CompileSynchronously = true)]
    public struct VehicleOnGroundJob : IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<float> speeds;
        [ReadOnly]
        public ComponentDataFromEntity<MovingData> movingData;
        [ReadOnly]
        public NativeList<Entity> entities;

        [NativeDisableParallelForRestriction]
        public ComponentDataFromEntity<Translation> translations;
        [NativeDisableParallelForRestriction]
        public ComponentDataFromEntity<Rotation> rotations;
        [ReadOnly]
        public NativeArray<RaycastHit_Normal> resultGround;

        //[WriteOnly]
        //public NativeArray<float4x4> localToWorldMatrix;

        public void Execute(int index, TransformAccess transform)
        {
            if (speeds[index] > 0)
            {
                Entity e = entities[index];
                float3 position = translations[e].Value;
                quaternion rotation = rotations[e].Value;
                RaycastHit_Normal r = resultGround[index];
                if (RayCastHitConvert.GetColliderID(r) != 0)
                {
                    float3 normal = r.normal;
                    float3 forward = math.mul(rotation, new float3(0, 0, 1));
                    rotation = quaternion.LookRotation(forward, normal);
                    position.y = r.point.y+ movingData[e].height;
                    translations[e] = new Translation { Value = position };
                    rotations[e] = new Rotation { Value = rotation };
                }
                transform.position = position;
                transform.rotation = rotation;
            }
        }
    }
}
