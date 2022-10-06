using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.Entities;
using TrafficLightNS;

[RequireComponent(typeof(Collider))]
public class IntersectionRoad : MonoBehaviour
{
    public TrafficLight trafficLight;
    public Queue<DynamicControllerBase> waitingDynamics;
    public Entity entity;
    TrafficLightsSystem system;

    [HideInInspector]
    public List<int> pedestrianLayers;
    [HideInInspector]
    public List<int> vehicleLayers;

    private Coroutine releaseCoroutine;
    // Start is called before the first frame update
    void Start()
    {
        waitingDynamics = new Queue<DynamicControllerBase>(5);
        if (!GetComponent<Collider>().isTrigger)
            Debug.LogException(new Exception("This collider for IntersectionRoad must be trigger"));
        //InvokeRepeating("TestTraffic", 1, 5);
        trafficLight.setLightIndex(TrafficLight.LightIndex.RED);

        system = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<TrafficLightsSystem>();
        entity = system.createEntity(this);
    }

    private void TestTraffic()
    {
        if (trafficLight.getCurrentLightIndex() == TrafficLight.LightIndex.GREEN)
            trafficLight.setLightIndex(TrafficLight.LightIndex.RED);
        //else trafficLight.setLightIndex(TrafficLight.LightIndex.GREEN);

        Release(5f);
    }
    public void SetLightIndex(TrafficLight.LightIndex index, float duration)
    {
        trafficLight.setLightIndex(index);
        system.CommandBuffer.SetComponent<CurrentLight>(entity, new CurrentLight { Value = index });
        Release(duration);
    }

    public TrafficLight.LightIndex getCurrentLight()
    {
        return trafficLight.getCurrentLightIndex();
    }

    public void Release(float duration)
    {
        if (releaseCoroutine != null)
            StopCoroutine(releaseCoroutine);
        if (trafficLight.getCurrentLightIndex() == TrafficLight.LightIndex.GREEN)
            releaseCoroutine = StartCoroutine(ReleaseWaitingDynamics(duration, vehicleLayers));
        else if (trafficLight.getCurrentLightIndex() == TrafficLight.LightIndex.RED)
            releaseCoroutine = StartCoroutine(ReleaseWaitingDynamics(duration, pedestrianLayers));
    }

    [ExecuteInEditMode]
    IEnumerator ReleaseWaitingDynamics(float duration, List<int> layers)
    {
        float time = Time.realtimeSinceStartup;
        float maxTime = time + duration;
        while (time < maxTime || duration < 0)
        {
            /*if (layers == pedestrianLayers)
                Debug.Log("relase pedes " + duration );
            else Debug.Log("relase veh " + duration);*/
            if (waitingDynamics != null && waitingDynamics.Count > 0)
            {
                var x = this.waitingDynamics.Dequeue();
                if (layers.Contains(x.gameObject.layer))
                    x.setForceStop( false);
                else waitingDynamics.Enqueue(x);
            }
            yield return new WaitForSecondsRealtime(1);
            time = Time.realtimeSinceStartup;
        }
        yield return null;
    }

    //ThisLogicOnly for pedestrian
    public void OnTriggerEnter(Collider other)
    {
        OnTriggerPedestrian(other);
    }

    public void OnTriggerPedestrian(in Collider other)
    {
        int layer = other.gameObject.layer;
        if (pedestrianLayers.Contains(layer))
        {
            if (trafficLight.getCurrentLightIndex() == TrafficLight.LightIndex.RED)
                return;

            var obj = other.GetComponent<DynamicControllerBase>();
            if (obj.setForceStop(true) && !waitingDynamics.Contains(obj))
                waitingDynamics.Enqueue(obj);
        }
    }

    public void OnTriggerVehicle(in Collider other)
    {
        int layer = other.gameObject.layer;
        if (vehicleLayers.Contains(layer))
        {
            if (trafficLight.getCurrentLightIndex() == TrafficLight.LightIndex.GREEN ||
                MathUltilities.Ultilities_Burst.Angle2(other.transform.forward, transform.forward) > 90)
                return;

            var obj = other.GetComponent<DynamicControllerBase>();
            if (obj.setForceStop(true) && !waitingDynamics.Contains(obj))
                waitingDynamics.Enqueue(obj);
        }
    }
}
