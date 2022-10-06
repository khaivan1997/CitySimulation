
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WalkingPath;

//using WalkingPath;
#if UNITY_EDITOR
public class RaycastTest : MonoBehaviour
{
    public Camera cam;
    public LayerMask raycastMask;
    public Text positionText;

    public Circle circle;
    public Sidewalk sidewalk;
    [EditorReadOnly]
    public float3 center;
    [EditorReadOnly]
    public float3[] v;
    Collider[] colliders;
    // Start is called before the first frame update
    void Start()
    {
        v = new float3[3];
        circle = new Circle();
        colliders = new Collider[3];
    }

    // Update is called once per frame
    void Update()
    {
        RaycastHit hitInfo = new RaycastHit();
        bool isHit = Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hitInfo, Mathf.Infinity, raycastMask);
        if (isHit)
            positionText.text = "point:" + hitInfo.point;
        else positionText.text = "no hit";
#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            /*var raycast_results = new NativeArray<RaycastHit>(3, Allocator.TempJob);
            var raycast_commands = new NativeArray<CapsulecastCommand>(3, Allocator.TempJob);*/
            var walkingSystem = WalkingPath.WalkingSystem.instance;
            if (isHit)
            {
                var node = walkingSystem.walkingGraph.GetNearestForce(hitInfo.point, Pathfinding.NNConstraint.Default).node as Pathfinding.TriangleMeshNode;
                center = (Vector3)node.position;
                Pathfinding.Int3 v0_temp, v1_temp, v2_temp;
                node.GetVertices(out v0_temp, out v1_temp, out v2_temp);
                v[0] = (Vector3)v0_temp;
                v[1] = (Vector3)v1_temp;
                v[2] = (Vector3)v2_temp;
                MathUltilities.Ultilities_Burst.getIncircle(v[0], v[1], v[2], out circle);
                center = circle.center;
                float radius = circle.radius;


                Debug.Log("hit at raycast " + hitInfo.transform.name + " id " + hitInfo.transform.GetInstanceID());
                Debug.Log("hit at node " + center + " index:" + node.NodeIndex + " circle " + circle);
                Debug.Log(" graphNode Area " + walkingSystem.graphNodes[node.NodeIndex].mapArea.ToString() + " recastArea " + node.Area);
                //Debug.Log("city road " + CityGen3D.Map.Instance.mapRoads.GetMapRoadAtWorldPosition(center.x, center.z, WalkingPath.WalkingSystem.ROAD_SCANNING_RADIUS));
                //Debug.Log("city area " + CityGen3D.Map.Instance.mapSurfaces.GetMapSurfaceAtWorldPosition(center.x, center.z) +" "+
                //CityGen3D.Map.Instance.mapSurfaces.GetMapSurfaceAtWorldPosition(center.x, center.z).type);
                using (var it = walkingSystem.graphConnections.GetValuesForKey(node.NodeIndex))
                {
                    while (it.MoveNext())
                        Debug.Log(" next " + it.Current + "area " + walkingSystem.graphNodes[it.Current.destinationIndex].mapArea.ToString());
                    //+ " has same sidewalk "+ walkingSystem.nodesHasSameSidewalk(node.NodeIndex, it.Current.destinationIndex));
                }


                // code for old implementation
                //int numOfColliders = Physics.OverlapSphereNonAlloc(center, radius, colliders, walkingSystem.pavementLayers, QueryTriggerInteraction.UseGlobal);
                //for(int i = 0 ; i < numOfColliders; i++)
                //{
                //    var c = colliders[i];
                //    Debug.Log("collider " +c.name+" "+ c.transform.GetInstanceID());
                //}

                //NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(numOfColliders, Allocator.TempJob);
                //NativeMultiHashMap<int, WalkingPath.Sidewalk> sidewalks = new NativeMultiHashMap<int, WalkingPath.Sidewalk>(2, Allocator.TempJob);

                //WalkingPath.WalkingSystem_Ultilities.calculateSideWalkPoints(v, colliders, numOfColliders, jobs, sidewalks.AsParallelWriter(), 0);
                //JobHandle.CompleteAll(jobs);
                //for (int t = 0; t < numOfColliders; t++)
                //{
                //    var key = sidewalks.GetValuesForKey(0);
                //    while (key.MoveNext())
                //    {
                //        var c = key.Current;
                //        Debug.Log(c);
                //        sidewalk = c;
                //    }
                //}
                //jobs.Dispose();
                //sidewalks.Dispose();

                //NativeList<int> neighbors = new NativeList<int>(9, Allocator.Temp);
                //WalkingPath.WalkingSystem_Ultilities.GatherNeighbors(walkingSystem.graphNodes, walkingSystem.graphConnections, node.NodeIndex, 3, neighbors);
                //foreach (var i in neighbors)
                //{
                //    Debug.Log("neighjbor " + i + " cut " + WalkingPath.WalkingSystem_Ultilities_Burst.isLineIntersectTriangleXZ(sidewalk.positionInner, sidewalk.positionOuter,
                //                v[0],v[1],v[2]));
                //}
                //neighbors.Dispose();
            }
            /*raycast_commands.dispose();
            raycast_results.dispose();*/

            //Test_PathFindingSpeed();
            //Test_RayCastCommand();
        }
    }

    private void Test_RayCastCommand()
    {
        var x = new NativeArray<RaycastCommand>(1, Allocator.TempJob);
        var res = new NativeArray<RaycastHit>(1, Allocator.TempJob);

        var r = new RaycastHit();
        r.point = Vector3.up;
        res[0] = r;
        Debug.Log($"bef {res[0].point}");
        RaycastCommand.ScheduleBatch(x, res, 1).Complete();
        Debug.Log($"aft{res[0].point}");
        x.Dispose();
        res.Dispose();
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(center, v[0] - center);
            Gizmos.DrawRay(center, v[1] - center);
            Gizmos.DrawRay(center, v[2] - center);
            Gizmos.DrawWireSphere(circle.center, circle.radius);

            if (sidewalk.colliderId != -1)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(sidewalk.positionInner, sidewalk.positionOuter);
            }

        }
    }
    public void Test_PathFindingSpeed()
    {
        //Benchmark path finding with containers
        var walkingSystem = WalkingSystem.instance;
        NativeList<float3> path = new NativeList<float3>(1000, Allocator.TempJob);
        NativeList<int> nodes = new NativeList<int>(1000, Allocator.TempJob);
        var time = Time.realtimeSinceStartup;
        int node_start = 1;
        int node_end = 129000;
       // walkingSystem.calculatePath(node_start, node_end, path, nodes).Complete();
        int index = 37;
        Debug.Log($"calculation from {node_start} to {node_end} takes {Time.realtimeSinceStartup - time} s");
        Debug.Log($"path length {nodes.Length} at node {nodes[index]}");
        path.Dispose();
        nodes.Dispose();

        path = new NativeList<float3>(1000, Allocator.TempJob);
        nodes = new NativeList<int>(1000, Allocator.TempJob);
        time = Time.realtimeSinceStartup;
        // walkingSystem.calculatePathWithHeap(node_start, node_end, path, nodes).Complete();
        Debug.Log($"calculation with heap from {node_start} to {node_end} takes {Time.realtimeSinceStartup - time} s");
        Debug.Log($"path length {nodes.Length} at node {nodes[index]}");
        path.Dispose();
        nodes.Dispose();
    }

    public void Test_MyNativeHeap()
    {
        var heap = new MyNativeHeap<int, comparer>(10, Allocator.Temp);
        int[] test = { 15,12,8,1,2,4,5};
        int key = -1;
        for(int i = 0; i< test.Length; i++)
        {
            if (test[i] == 1)
                key = heap.Insert(test[i]);
            else heap.Insert(test[i]);
        }
        heap.PrintOut();

        heap.Remove(key);
        Debug.Log($"after remove 1, {heap.Length}");
        heap.PrintOut();
        heap.Dispose();
    }

    struct comparer : IComparer<int>
    {
        public int Compare(int x, int y)
        {
            return x - y;
        }
    }

#endif
}
#endif
