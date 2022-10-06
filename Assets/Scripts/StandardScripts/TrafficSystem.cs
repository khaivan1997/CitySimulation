

namespace VehicleNS
{
    using System;
    using UnityEngine;
    using UnityEngine.Jobs;
    using Unity.Collections;
    using TurnTheGameOn.SimpleTrafficSystem;
    using Unity.Jobs;
    using Unity.Burst;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Collections.LowLevel.Unsafe;

    public class TrafficSystem : MonoBehaviour
    {
        public WaypointsSystem WaypointsSystem;
        public static TrafficSystem instance;

        // Start is called before the first frame update
        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                instance = this;
            }
        }

        public JobHandle calculatePath(Vector3 startPoint, Vector3 endPoint, in DynamicBuffer<VehiclePathBuffer> pathBuffers)
        {
            AITrafficWaypoint startWaypoint = MyUtilities.findClosetWayPoint(startPoint, 50f).GetComponent<AITrafficWaypoint>();
            AITrafficWaypoint endWaypoint = MyUtilities.findClosetWayPoint(endPoint, 50f).GetComponent<AITrafficWaypoint>();

            var calculateWaypointsJob = new CalculateWaypointsVehicleJob
            {
                startPoint = WaypointsSystem.FindWayPointData(this.WaypointsSystem.roadsMapForJob,
                    this.WaypointsSystem.FindWaypointKey(startWaypoint)),
                endPoint = WaypointsSystem.FindWayPointData(this.WaypointsSystem.roadsMapForJob,
                    this.WaypointsSystem.FindWaypointKey(endWaypoint)),
                roadsMapForJob = this.WaypointsSystem.roadsMapForJob,
                waypoints = pathBuffers,
            };
            return calculateWaypointsJob.Schedule<CalculateWaypointsVehicleJob>();
            /*calculateWaypointsJob.Execute();
            return default;*/
        }
    }


    [BurstCompile(CompileSynchronously = true)]
    public struct CalculateWaypointsVehicleJob : IJob
    {
        public struct Visited: IEquatable<Visited>
        {
            public int streetIndex;
            public int laneIndex;

            public bool Equals(Visited other)
            {
                return this == other;
            }

            public static bool operator ==(Visited c1, Visited c2)
            {
                return c1.streetIndex == c2.streetIndex && c1.laneIndex == c2.laneIndex;
            }

            public static bool operator !=(Visited c1, Visited c2)
            {
                return !(c1 == c2);
            }
        }

        public Waypoint startPoint;
        public Waypoint endPoint;
        [ReadOnly]
        public NativeHashMap<int, Road> roadsMapForJob;
        //result
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public DynamicBuffer<VehiclePathBuffer> waypoints;
        public void Execute()
        {
            NativeList<Visited> visiteds = new NativeList<Visited>(roadsMapForJob.Capacity, Allocator.Temp);
            waypoints.AddRange(this.calculatePath(startPoint, endPoint, visiteds));
            visiteds.Dispose();
        }

        public NativeList<VehiclePathBuffer> calculatePath(Waypoint startWaypoint, Waypoint endWaypoint, NativeList<Visited> visiteds)
        {
            NativeList<VehiclePathBuffer> path = new NativeList<VehiclePathBuffer>(Allocator.Temp);
            var visted = new Visited { streetIndex = startWaypoint.currentKey.roadId, laneIndex = startWaypoint.currentKey.laneIndex };
            if (visiteds.Contains<Visited,Visited>(visted))
                return path;
            Lane lane = roadsMapForJob[visted.streetIndex].lanes.Value.arrays[visted.laneIndex];
            visiteds.Add(visted);
            int currentIndex = startWaypoint.currentKey.waypointIndex;
            int endIndex = endWaypoint.currentKey.waypointIndex;
            if (isLaneConsistWaypoint(ref lane, endWaypoint))
            {
                ref var waypoints = ref lane.waypoints.Value.arrays;
                int i = currentIndex;
                while (i <= endIndex)
                {
                    path.Add(new VehiclePathBuffer { position = waypoints[i].position });
                    i++;
                }
            }
            else
            {
                NativeList<VehiclePathBuffer> nextPath = new NativeList<VehiclePathBuffer>(5, Allocator.Temp);
                NativeList<Lane> alias = findAliasLane(lane);
                for(int l = -1; l < alias.Length; l++)
                {
                    ref BlobArray<Waypoint> laneWayPoints = ref lane.waypoints.Value.arrays; 
                    if(l >= 0)
                        laneWayPoints = ref alias[l].waypoints.Value.arrays;

                    Waypoint routeEndPoint = laneWayPoints[laneWayPoints.Length - 1];
                    ref var nextRoutePoints = ref routeEndPoint.nextRoadWaypointKeys.Value.arrays;
                    for (int index = 0; index < nextRoutePoints.Length; index++)
                    {
                        //JobLogger.Log("at next key ", nextRoutePoints[index]);
                        nextPath = this.calculatePath(WaypointsSystem.FindWayPointData(this.roadsMapForJob, nextRoutePoints[index]), endWaypoint, visiteds);
                        if (nextPath.Length > 0)
                        {

                            int i = currentIndex;
                            while (i < laneWayPoints.Length)
                            {
                                path.Add(new VehiclePathBuffer {position = laneWayPoints[i].position });
                                i++;
                            }
                            path.AddRange(nextPath);
                            break;
                        }
                    }
                    if (nextPath.Length > 0)
                        break;
                }
                alias.Dispose();
                nextPath.Dispose();
            }
            return path;
        }

        bool isLaneConsistWaypoint( ref Lane lane, Waypoint waypoint)
        {
            ref BlobArray<Waypoint> waypoints = ref lane.waypoints.Value.arrays;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (math.all(waypoints[i].position == waypoint.position))
                    return true;
            }
               
            return false;
        }

        NativeList<Lane> findAliasLane(in Lane lane)
        {
            NativeList<Lane> result = new NativeList<Lane>(2, Allocator.Temp);
            var key = lane.waypoints.Value.arrays[0].currentKey;
            Road r = roadsMapForJob[key.roadId];
            ref var lanes = ref r.lanes.Value.arrays;
            for(int i = 0; i < lanes.Length; i++)
            {
                if (lanes[i] != lane && (lanes[i] & lane))
                    result.Add(lanes[i]);
            }
            return result;
        }
    }
}
 
