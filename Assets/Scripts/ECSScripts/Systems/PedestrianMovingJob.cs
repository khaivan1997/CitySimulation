using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Transforms;
using Unity.Entities;
using RaycastHit_Normal = UnityEngine.RaycastHit;

namespace WalkingPath
{
    [BurstCompile(CompileSynchronously = true)]
    public struct PedestrianOnGroundJob : IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<float> speeds;
        //[ReadOnly]
        //public NativeArray<float> heights;
        [ReadOnly]
        public NativeList<Entity> entities;

        [NativeDisableParallelForRestriction]
        public ComponentDataFromEntity<Translation> translations;
        [ReadOnly]
        public ComponentDataFromEntity<Rotation> rotations;
        [ReadOnly]
        public NativeArray<RaycastHit_Normal> resultGround;

        [WriteOnly]
        public NativeArray<float4x4> localToWorldMatrix;
        
        public void Execute(int index, TransformAccess transform)
        {
            Entity e = entities[index];
            float3 position = translations[e].Value;
            if (speeds[index]>0)
            {  
                RaycastHit_Normal r = resultGround[index];
                if(RayCastHitConvert.GetColliderID(r) != 0)
                {
                    position.y = r.point.y;// + height / 2f;
                    translations[e] = new Translation { Value = position };
                }               
                transform.rotation = rotations[e].Value;
            }
            transform.position = position;
            localToWorldMatrix[index] = transform.localToWorldMatrix;
        }
    }

}
