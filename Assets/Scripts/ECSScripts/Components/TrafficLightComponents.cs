using Unity.Entities;
using Unity.Mathematics;

namespace TrafficLightNS
{
    public struct TrafficLightTag: IComponentData    {  }
    public struct CurrentLight : IComponentData
    {
        public TrafficLight.LightIndex Value;
    }
}
