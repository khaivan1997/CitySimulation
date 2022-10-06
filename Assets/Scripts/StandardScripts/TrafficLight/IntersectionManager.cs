using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IntersectionManager : MonoBehaviour
{
    [System.Serializable]
    public struct Lanes
    {
        public IntersectionRoad[] roads;
        public float greenTimer;

    }

    public float yellowTimer = 3f;
    public LayerMask pedestrianMask;
    public LayerMask vehicleMask;
    public Lanes[] ways;
    int currentWay = -1;
    Coroutine cycleCoroutine;
    public void Start()
    {
        for (int i = 0; i < ways.Length; i++)
        {
            ChangeLanes(ways[i], TrafficLight.LightIndex.RED, 0f);
        }
        setUpLayers();
        cycleCoroutine = StartCoroutine(CycleTrafficLight());
    }

    public void StartAutoCycle()
    {
        if (cycleCoroutine == null)
            cycleCoroutine = StartCoroutine(CycleTrafficLight());
    }

    public void StopAutoCycle()
    {
        if (cycleCoroutine != null)
        {
            StopCoroutine(cycleCoroutine);
            cycleCoroutine = null;
        }

    }

    public void ChangeWayEditor(float duration)
    {
        this.StopAutoCycle();
        currentWay = (++currentWay) % (ways.Length);
        ChangeWay(duration);
    }

    void setUpLayers()
    {
        for (int i = 0; i < ways.Length; i++)
        {
            var roads = ways[i].roads;
            for (int j = 0; j < roads.Length; j++)
            {
                roads[j].pedestrianLayers = MyUtilities.convertMaskToLayers(pedestrianMask);
                roads[j].vehicleLayers = MyUtilities.convertMaskToLayers(vehicleMask); ;
            }
        }
    }


    void ChangeLanes(in Lanes lanes, TrafficLight.LightIndex lightIndex, float duration = 0)
    {
        var roads = lanes.roads;
        for (int j = 0; j < roads.Length; j++)
        {
            roads[j].SetLightIndex(lightIndex, duration);
        }
    }

    public void AllRed()
    {
        this.StopAutoCycle();
        for (int i = 0; i < ways.Length; i++)
        {
            ChangeLanes(ways[i], TrafficLight.LightIndex.RED, -1);
        }
    }

    public void AllGreen()
    {
        this.StopAutoCycle();
        for (int i = 0; i < ways.Length; i++)
        {
            ChangeLanes(ways[i], TrafficLight.LightIndex.GREEN, -1);
        }
    }

    public void ChangeWay(float duration)
    {
        for (int i = 0; i < ways.Length; i++)
        {
            if (i == currentWay)
                ChangeLanes(ways[i], TrafficLight.LightIndex.GREEN, duration - yellowTimer);
            else
                ChangeLanes(ways[i], TrafficLight.LightIndex.RED, duration);
        }
    }

    public void setCurrenWay(TrafficLight.LightIndex lightIndex)
    {
        ChangeLanes(ways[currentWay], lightIndex);
    }

    [ExecuteInEditMode]
    IEnumerator CycleTrafficLight()
    {
        while (true)
        {
            currentWay = (++currentWay) % (ways.Length);
            float duration = ways[currentWay].greenTimer;
            ChangeWay(duration);
            yield return new WaitForSecondsRealtime(duration - yellowTimer);
            setCurrenWay(TrafficLight.LightIndex.YELLOW);
            yield return new WaitForSecondsRealtime(yellowTimer);
        }

    }
#if UNITY_EDITOR
    public void ResetAllMaterials()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        for(int i = 0; i < renderers.Length; i++)
        {
            Material m;
            if (renderers[i].name.ToLower().Contains("red"))
                m = Resources.Load<Material>("GameMaterials/TrafficLightRed");
            else if (renderers[i].name.ToLower().Contains("yellow"))
                m = Resources.Load<Material>("GameMaterials/TrafficLightYellow");
            else
                m = Resources.Load<Material>("GameMaterials/TrafficLightGreen");
            Debug.Log("got material " + m.name + " and "+ (m == renderers[i].sharedMaterial));
            renderers[i].sharedMaterial = m;
        }
    }
#endif
}
