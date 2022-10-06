using Unity.Entities;
using Unity.Mathematics;
using Dynamics; 

namespace VehicleNS
{
    [InternalBufferCapacity(400)]
    public struct VehiclePathBuffer : IBufferElementData
    {
        public float3 position;
    }

    public struct VehicleTag : IComponentData
    {
        public int index;
    }
}
