using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

public class PathCalculationSystem : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        var graphs = AstarPath.active.graphs;
        foreach (var graph in graphs)
        {
            graph.Scan();
            Debug.Log("graph scan "+ graph.name);
        }
            
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
