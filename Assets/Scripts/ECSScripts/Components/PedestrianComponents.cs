
using Unity.Entities;
using Unity.Mathematics;

namespace WalkingPath
{
    [InternalBufferCapacity(400)]
    public struct WalkingPathBuffer : IBufferElementData
    {
        public float3 position;
        public int node_index;
    }

    public struct PedestrianTag : IComponentData
    {
        public int index;
    }

    public struct PedestrianType: ISharedComponentData
    {
        public PedestrianCategory category;
    }

    public enum PedestrianCategory
    {
        MOVING = 0,
        MOVING_FAST = 1,
        STATIC = 2,
    }
}
