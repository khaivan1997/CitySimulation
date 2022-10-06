using Unity.Entities;

namespace Dynamics
{
    public interface ITag:IComponentData
    {
        public int getIndex();
    }

    public struct MovingData : IComponentData
    {
        public uint mask_unavoidableObstacles;
        public uint mask_avoidableObstacles;
        public float radius;
        public float speed;
        public float stopDistance;
        public float height;
        public float max_steering_angle;
        public float max_speed;
    }

    public struct OnWaitingForPath : IComponentData
    { }

    public struct Inactive : IComponentData
    { }

    public struct OnDestinationReach : IComponentData
    { }

    public struct PathProgression : IComponentData
    {
        public int pathIndex;
        //public byte onStop;
    }
}
