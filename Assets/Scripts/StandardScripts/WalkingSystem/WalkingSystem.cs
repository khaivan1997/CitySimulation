using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Entities;

namespace WalkingPath
{
    //This class is SIngleTon
    [System.Serializable]
    public class WalkingSystem : MonoBehaviour
    {
        public static WalkingSystem instance;

        public static uint TolerantCost = 0;
        public const float halfCharacterHeight = 1f;
        public const float characterRadius = 1f;
        public const float maxSlope = 30f;
        public const float PAVEMENT_WIDTH = CityGen3D.MapRoad.SIDEWALK_WIDTH;
        public const float ROAD_SCANNING_RADIUS = 1f;

        public const uint COST_CROSS_ROAD = 5000;
        public const uint COST_CROSS_NO_FOOTPATH = 100;
        public const uint COST_MINIMUM = 10;
        public const uint COST_INTERSECIOTN = 10;
        public const uint COST_CROSS_AREA = 100000;

        public enum ROAD_WALKING_TYPE { FOOTPATH = 4, TRACK = 5 };
        public enum LANDSCAPE_NON_WALKING_TYPE { WATER = 15 };
        public enum LANDSCAPE_PRIORITY_WALKING_TYPE { COMMERCIAL = 2, FOOTWAY = 27 }

        public LayerMask staticObstacleLayers;
        public LayerMask pavementLayers;
        public LayerMask intersectionLayers;

        public NativeArray<MyGraphNode> graphNodes;
        public NativeMultiHashMap<int, MyConnection> graphConnections;
        public NativeMultiHashMap<int, Sidewalk> graphNodes_sidewalks;
        public RecastGraph walkingGraph;

        public GameObject map;
        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            else
            {
                instance = this;
            }
            this.buildMultiThreadGraph();
            if(map!= null) map.SetActive(false);
        }


        private void OnDestroy()
        {
            this.graphNodes.Dispose();
            this.graphConnections.Dispose();
            this.graphNodes_sidewalks.Dispose();
        }

        void addStaticObstacleModifier()
        {
            var goArray = MyUtilities.FindGameObjectsOfTypeWithLayer<Renderer>(staticObstacleLayers);
            if (goArray != null)
            {
                for (int i = 0; i < goArray.Length; i++)
                {
                    var com = goArray[i].AddComponent<RecastMeshObj>();
                    com.area = -1;
                    com.dynamic = false;
                }
            }
        }

        void addPavementModifier()
        {
            var goArray = MyUtilities.FindGameObjectsOfTypeWithLayer<MeshCollider>(pavementLayers);
            if (goArray != null)
            {
                for (int i = 0; i < goArray.Length; i++)
                {
                    var com = goArray[i].AddComponent<RecastMeshObj>();
                    com.area = 10;
                    com.dynamic = false;
                }
            }
        }

        private void EnhanceIntersectionCollider(GameObject[] goArray)
        {
            if (goArray != null)
            {
                for (int i = 0; i < goArray.Length; i++)
                {
                    var col = goArray[i].GetComponent<BoxCollider>();
                    Vector3 size = col.size;
                    size.y = 3f;
                    col.size = size;
                    col.transform.Translate(1.2f * col.transform.up);
                }
            }
        }
        public void buildMultiThreadGraph()
        {
            addStaticObstacleModifier();
            addPavementModifier();

            #region intersectionModifier
            var goArray = MyUtilities.FindGameObjectsOfTypeWithLayer<IntersectionRoad>(intersectionLayers);
            if (goArray != null)
            {
                for (int i = 0; i < goArray.Length; i++)
                {
                    var com = goArray[i].AddComponent<RecastMeshObj>();
                    com.area = 1;
                    com.dynamic = false;
                }
            }
            #endregion

            walkingGraph = (RecastGraph)AstarPath.active.graphs[0];
            walkingGraph.SnapForceBoundsToScene();
            walkingGraph.Scan();
            //EnhanceIntersectionCollider(goArray);
            var time = Time.realtimeSinceStartup;
            List<GraphNode> nodes = new List<GraphNode>();
            walkingGraph.GetNodes(nodes.Add);

            this.graphNodes = new NativeArray<MyGraphNode>(nodes.Count + 1, Allocator.Persistent);
            this.graphConnections = new NativeMultiHashMap<int, MyConnection>(nodes.Count + 1, Allocator.Persistent);
            this.graphNodes_sidewalks = new NativeMultiHashMap<int, Sidewalk>(nodes.Count + 1, Allocator.Persistent);
            NativeMultiHashMap<int, Sidewalk> graphNodes_sidewalks_temp = new NativeMultiHashMap<int, Sidewalk>(nodes.Count, Allocator.TempJob);

            var sideWalkJobHandles = new NativeList<JobHandle>(nodes.Count, Allocator.TempJob);

            Collider[] colliders = new Collider[3];
            int numOfColliders;
            for (int i = 0; i < nodes.Count; i++)
            {
                //ParsingNode
                TriangleMeshNode mNode = (TriangleMeshNode)nodes[i];
                Int3 v0_temp, v1_temp, v2_temp;
                mNode.GetVertices(out v0_temp, out v1_temp, out v2_temp);
                float3[] v = new float3[3];
                v[0] = (Vector3)v0_temp;
                v[1] = (Vector3)v1_temp;
                v[2] = (Vector3)v2_temp;
                var inCirlce = new Circle();
                MathUltilities.Ultilities_Burst.getIncircle(v[0], v[1], v[2], out inCirlce);
                Vector3 center = inCirlce.center;
                float radius = inCirlce.radius;

                MapArea mapArea = MapArea.LANDSCAPE;
                numOfColliders = Physics.OverlapSphereNonAlloc(center, radius, colliders, intersectionLayers, QueryTriggerInteraction.UseGlobal);
                if (numOfColliders > 0)
                    mapArea = MapArea.ROAD_INTERSECTION;
                else
                {
                    CityGen3D.MapRoad road = CityGen3D.Map.Instance.mapRoads.GetMapRoadAtWorldPosition(center.x, center.z, ROAD_SCANNING_RADIUS);
                    if (road != null)
                    {
                        if (!EnumUtil<ROAD_WALKING_TYPE>.ConsistsInt(road.type))
                            mapArea = MapArea.ROAD;
                        else
                            mapArea = MapArea.FOOTPATH;
                    }
                }

                if (mapArea == MapArea.ROAD || mapArea == MapArea.ROAD_INTERSECTION)
                {
                    numOfColliders = Physics.OverlapSphereNonAlloc(center, radius, colliders, pavementLayers, QueryTriggerInteraction.UseGlobal);
                    if (numOfColliders > 0)
                    {
                        if (mapArea == MapArea.ROAD_INTERSECTION)
                            mapArea = MapArea.ROAD_SIDEWALK_INTERSECTION;
                        else
                        {
                            mapArea = MapArea.ROAD_SIDEWALK;
                            NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(numOfColliders, Allocator.TempJob);
                            calculateSideWalkPoints(v, colliders, numOfColliders, jobs, graphNodes_sidewalks_temp.AsParallelWriter(), mNode.NodeIndex);
                            sideWalkJobHandles.AddRange(jobs);
                            jobs.Dispose();
                        }
                    }
                }
                else
                {
                    CityGen3D.MapSurface sur = CityGen3D.Map.Instance.mapSurfaces.GetMapSurfaceAtWorldPosition(center.x, center.z);
                    if (EnumUtil<LANDSCAPE_PRIORITY_WALKING_TYPE>.ConsistsInt(sur.type))
                    {
                        mapArea = MapArea.FOOTPATH;
                    }
                }

                graphNodes[(mNode.NodeIndex)] = new MyGraphNode
                {
                    vertex0 = v[0],
                    vertex1 = v[1],
                    vertex2 = v[2],
                    inCircle = inCirlce,
                    graphArea = mNode.Area,
                    mapArea = mapArea,
                    internalIndex = (mNode.NodeIndex),
                };
            }

            //ParsingConnection
            for (int i = 0; i < nodes.Count; i++)
            {
                TriangleMeshNode mNode = (TriangleMeshNode)nodes[i];
                Connection[] connections = mNode.connections;
                for (int j = 0; j < connections.Length; j++)
                {
                    var con = connections[j];
                    uint cost = COST_MINIMUM;
                    if (mNode.Area != con.node.Area)
                        cost = COST_CROSS_AREA;
                    else if (graphNodes[(con.node.NodeIndex)].mapArea == MapArea.ROAD)
                        cost = COST_CROSS_ROAD;
                    else if (graphNodes[(con.node.NodeIndex)].mapArea == MapArea.LANDSCAPE)
                        cost = COST_CROSS_NO_FOOTPATH;
                    else if (graphNodes[(con.node.NodeIndex)].mapArea == MapArea.ROAD_INTERSECTION)
                        cost = COST_INTERSECIOTN;

                    //Debug.Log("at Node " + mNode.NodeIndex + " to " + con.node.NodeIndex);
                    this.graphConnections.Add((mNode.NodeIndex), new MyConnection
                    {
                        destinationIndex = (con.node.NodeIndex),
                        cost = cost + (con.cost / 1000u)
                    });
                }
            }

            //interpolate sidewalks points
            var keys = graphNodes_sidewalks_temp.GetKeyArray(Allocator.TempJob);

            new InterpolateSidewalkJob
            {
                graphNodes = graphNodes,
                graphConnections = graphConnections,
                keys = keys,
                graphNodes_sidewalks = graphNodes_sidewalks_temp,
                graphNodes_sidewalksWriter = graphNodes_sidewalks.AsParallelWriter(),
            }.ScheduleBatch<InterpolateSidewalkJob>(keys.Length, 128, JobHandle.CombineDependencies(sideWalkJobHandles))
            .Complete();

            sideWalkJobHandles.Dispose();

            //keys.Dispose();
            graphNodes_sidewalks_temp.Dispose();

            Debug.Log("time taken to parse walking in seconds " + (Time.realtimeSinceStartup - time));
            Debug.Log("got nodes" + this.graphNodes.Length + " " + this.walkingGraph.CountNodes());
        }

        public JobHandle calculatePath(in Vector3 startPoint, in Vector3 endPoint, in DynamicBuffer<WalkingPathBuffer> pathBuffers)
        {
            var node_start_index = (walkingGraph.GetNearestForce(startPoint, NNConstraint.Default).node.NodeIndex);
            var node_end_index = (walkingGraph.GetNearestForce(endPoint, NNConstraint.Default).node.NodeIndex);
            if (node_start_index == node_end_index)
                return default;
            var calculateWaypointsJob = new CalculateWaypointsWalkingJobWithHeap
            {
                startPoint_index = node_start_index,
                endPoint_index = node_end_index,
                graphConnections = graphConnections,
                graphNodes = graphNodes,
                graphNodesWalkableArea = graphNodes_sidewalks,
                startPoint = startPoint,
                endPoint = endPoint,
                tolerantDistance = TolerantCost,
                paths = pathBuffers,
            };
            return calculateWaypointsJob.Schedule<CalculateWaypointsWalkingJobWithHeap>();
        }

        public JobHandle calculatePath(in int node_start_index, in int node_end_index, in DynamicBuffer<WalkingPathBuffer> pathBuffers)
        {
            if (node_start_index == node_end_index)
                return default;
            var calculateWaypointsJob = new CalculateWaypointsWalkingJobWithHeap
            {
                startPoint_index = node_start_index,
                endPoint_index = node_end_index,
                graphConnections = graphConnections,
                graphNodes = graphNodes,
                graphNodesWalkableArea = graphNodes_sidewalks,
                startPoint = graphNodes[node_start_index].getCenter(),
                endPoint = graphNodes[node_end_index].getCenter(),
                tolerantDistance = TolerantCost,
                paths = pathBuffers,
            };
            return calculateWaypointsJob.Schedule<CalculateWaypointsWalkingJobWithHeap>();
        }

        public JobHandle calculatePathWithHeap(in int node_start_index, in int node_end_index, in DynamicBuffer<WalkingPathBuffer> pathBuffers)
        {
            if (node_start_index == node_end_index)
                return default;
            var calculateWaypointsJob = new CalculateWaypointsWalkingJobWithHeap
            {
                startPoint_index = node_start_index,
                endPoint_index = node_end_index,
                graphConnections = graphConnections,
                graphNodes = graphNodes,
                graphNodesWalkableArea = graphNodes_sidewalks,
                startPoint = graphNodes[node_start_index].getCenter(),
                endPoint = graphNodes[node_end_index].getCenter(),
                tolerantDistance = TolerantCost,
                paths = pathBuffers,
            };
            return calculateWaypointsJob.Schedule<CalculateWaypointsWalkingJobWithHeap>();
        }

        public bool drawWaypointGizmos = false;
        public void OnDrawGizmos()
        {
            if (drawWaypointGizmos)
            {
                Gizmos.color = Color.yellow;
                var keys = graphNodes_sidewalks.GetKeyArray(Allocator.Temp);
                for (int i = 0; i < keys.Length; i++)
                {
                    var it = graphNodes_sidewalks.GetValuesForKey(keys[i]);
                    while (it.MoveNext())
                    {
                        var c = it.Current;
                        Gizmos.DrawLine(c.positionInner, c.positionOuter);
                    }
                }
                keys.Dispose();
            }
        }

        //Length of results and jobs should be the same as # of colliders, vertices is 3 vertices of the triangle of the Navmesh
        public static void calculateSideWalkPoints(float3[] vertices, Collider[] colliders, int numOfCollider, NativeArray<JobHandle> jobs, NativeMultiHashMap<int, Sidewalk>.ParallelWriter graphNodes_sidewalks, int nodeIndex)
        {
            for (int i = 0; i < numOfCollider; i++)
            {
                var mesh = (colliders[i] as MeshCollider).sharedMesh;
                var subResult_original = new NativeQueue<float3>(Allocator.TempJob);
                var triangles = new NativeArray<int>(mesh.triangles, Allocator.TempJob);
                int lengthCount = triangles.Length / 3;
                var subResult = subResult_original.AsParallelWriter();
                var mJob = new FindSideWalkIntersectionJob
                {
                    vertex0 = vertices[0],
                    vertex1 = vertices[1],
                    vertex2 = vertices[2],
                    vertices = MyUtilities.GetNativeArrayFloat3fromVector3(mesh.vertices, Allocator.TempJob),
                    triangles = triangles,
                    localToWorldMatrix = colliders[i].transform.localToWorldMatrix,
                    subResult = subResult,
                };

                var finalJob = new FindSideWalkPointJob
                {
                    vertex0 = vertices[0],
                    vertex1 = vertices[1],
                    vertex2 = vertices[2],
                    subResult = subResult_original,
                    graphNodes_sidewalks = graphNodes_sidewalks,
                    resultIndex = nodeIndex,
                    colliderId = colliders[i].transform.GetInstanceID(),
                };
                var intersectionHandle = mJob.ScheduleBatch<FindSideWalkIntersectionJob>(lengthCount, 128);
                jobs[i] = finalJob.Schedule<FindSideWalkPointJob>(intersectionHandle);

                subResult_original.Dispose(jobs[i]);
                intersectionHandle.Complete();
            }
        }
    }
}


