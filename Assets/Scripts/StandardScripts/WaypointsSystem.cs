
namespace VehicleNS
{
    using System.Collections.Generic;
    using UnityEngine;
    using Unity.Collections;
    using TurnTheGameOn.SimpleTrafficSystem;
    using Unity.Entities;
    using Unity.Mathematics;
    using System.Runtime.InteropServices;

    [System.Serializable]
    public class WaypointsSystem : MonoBehaviour
    {
        public GameObject roadsContainer;
        public GameObject connenctorsContainer;
        public NativeHashMap<int, Road> roadsMapForJob;
        public Dictionary<GameObject, int> roadGameObjectToId;
        public Dictionary<int, GameObject> idToRoadGameObject;
        private int roadId;
        public string[] waypointLayers;

        // Start is called before the first frame update
        void Awake()
        {
            //Debug.Log("debug " + UnsafeUtility.IsBlittable<Road>());
            var time = Time.realtimeSinceStartup;
            int roadsCount = roadsContainer.transform.childCount + connenctorsContainer.transform.childCount;
            roadId = 0;
            this.roadGameObjectToId = new Dictionary<GameObject, int>(roadsCount);
            this.idToRoadGameObject = new Dictionary<int, GameObject>(roadsCount);
            foreach (Transform road in roadsContainer.transform)
            {
                roadId++;

                this.roadGameObjectToId.Add(road.gameObject, roadId);
                this.idToRoadGameObject.Add(roadId, road.gameObject);
            }

            foreach (Transform connectors in connenctorsContainer.transform)
            {
                roadId++;
                this.roadGameObjectToId.Add(connectors.gameObject, roadId);
                this.idToRoadGameObject.Add(roadId, connectors.gameObject);
            }

            this.roadsMapForJob = new NativeHashMap<int, Road>(roadsCount, Allocator.Persistent);
            foreach (Transform road in roadsContainer.transform)
            {
                NativeArray<Lane> lanes = new NativeArray<Lane>(road.childCount, Allocator.Temp);
                for (int laneIndex = 0; laneIndex < road.childCount; laneIndex++)
                {
                    AITrafficWaypointRoute lane = road.GetChild(laneIndex).GetComponent<AITrafficWaypointRoute>();
                    NativeArray<Waypoint> waypoints = new NativeArray<Waypoint>(lane.waypointDataList.Count, Allocator.Temp);
                    for (int waypointIndex = 0; waypointIndex < lane.waypointDataList.Count; waypointIndex++)
                    {
                        AITrafficWaypoint[] newRoutePoints = lane.waypointDataList[waypointIndex]._waypoint.onReachWaypointSettings.newRoutePoints;
                        NativeArray<WaypointKey> nextRoadWaypointKeys = new NativeArray<WaypointKey>(newRoutePoints.Length, Allocator.Temp);
                        for (int nextRoadIndex = 0; nextRoadIndex < newRoutePoints.Length; nextRoadIndex++)
                        {

                            nextRoadWaypointKeys[nextRoadIndex] = this.FindWaypointKey(newRoutePoints[nextRoadIndex]);

                        }
                        waypoints[waypointIndex] = new Waypoint
                        {
                            position = lane.waypointDataList[waypointIndex]._transform.position,
                            currentKey = this.FindWaypointKey(lane.waypointDataList[waypointIndex]._waypoint),
                            nextRoadWaypointKeys = MyUtilities.createReference<WaypointKey>(nextRoadWaypointKeys),
                        };
                        nextRoadWaypointKeys.Dispose();
                    }
                    lanes[laneIndex] = new Lane
                    {
                        waypoints = MyUtilities.createReference<Waypoint>(waypoints),
                    };
                    waypoints.Dispose();
                }
                int roadIndex = this.roadGameObjectToId[road.gameObject];
                roadsMapForJob.Add(roadIndex, new Road
                {
                    roadId = roadIndex,
                    type = RoadType.MAIN_ROAD,
                    lanes = MyUtilities.createReference<Lane>(lanes),
                });
                lanes.Dispose();
            }

            foreach (Transform connectors in connenctorsContainer.transform)
            {
                NativeArray<Lane> lanes = new NativeArray<Lane>(1, Allocator.Temp);
                AITrafficWaypointRoute lane = connectors.GetComponent<AITrafficWaypointRoute>();
                NativeArray<Waypoint> waypoints = new NativeArray<Waypoint>(lane.waypointDataList.Count, Allocator.Temp);
                for (int waypointIndex = 0; waypointIndex < lane.waypointDataList.Count; waypointIndex++)
                {
                    AITrafficWaypoint[] newRoutePoints = lane.waypointDataList[waypointIndex]._waypoint.onReachWaypointSettings.newRoutePoints;
                    NativeArray<WaypointKey> nextRoadWaypointKeys = new NativeArray<WaypointKey>(newRoutePoints.Length, Allocator.Temp);
                    for (int nextRoadIndex = 0; nextRoadIndex < newRoutePoints.Length; nextRoadIndex++)
                    {
                        nextRoadWaypointKeys[nextRoadIndex] = this.FindWaypointKey(newRoutePoints[nextRoadIndex]);
                    }
                    waypoints[waypointIndex] = new Waypoint
                    {
                        position = lane.waypointDataList[waypointIndex]._transform.position,
                        currentKey = this.FindWaypointKey(lane.waypointDataList[waypointIndex]._waypoint),
                        nextRoadWaypointKeys = MyUtilities.createReference<WaypointKey>(nextRoadWaypointKeys),
                    };
                }
                lanes[0] = new Lane
                {
                    waypoints = MyUtilities.createReference<Waypoint>(waypoints),
                };
                int roadIndex = this.roadGameObjectToId[connectors.gameObject];
                roadsMapForJob.Add(roadIndex, new Road
                {
                    roadId = roadIndex,
                    type = RoadType.CONNECTOR_ROAD,
                    lanes = MyUtilities.createReference<Lane>(lanes),
                }) ;
                waypoints.Dispose();
                lanes.Dispose();
            }
            Debug.Log("have " + roadId + " " + this.roadGameObjectToId.Count);
            Debug.Log("time taken to parse vehicle in seconds " + (Time.realtimeSinceStartup - time));
        }

        // Update is called once per frame

        public WaypointKey FindWaypointKey(AITrafficWaypoint waypoint)
        {
            //Debug.Log("with waypoint "+ waypoint.transform.position);
            int roadId = -1, laneId = -1;

            if (LayerMask.LayerToName(waypoint.gameObject.layer) == this.waypointLayers[0])
            {
                roadId = this.roadGameObjectToId[waypoint.onReachWaypointSettings.parentRoute.transform.parent.gameObject];
                laneId = waypoint.onReachWaypointSettings.parentRoute.transform.GetSiblingIndex();
            }
            else
            {
                roadId = this.roadGameObjectToId[waypoint.onReachWaypointSettings.parentRoute.gameObject];
                laneId = 0;
            }
            //Debug.Log("result " + roadId+" "+laneId);
            return (new WaypointKey
            {
                roadId = roadId,
                laneIndex = laneId,
                waypointIndex = waypoint.onReachWaypointSettings.waypointIndexnumber - 1,
            });

        }

        public static Waypoint FindWayPointData(NativeHashMap<int, Road> roadsMapForJob, WaypointKey key)
        {
            Road r;
            roadsMapForJob.TryGetValue(key.roadId, out r);
            return r.lanes.Value.arrays[key.laneIndex].waypoints.Value.arrays[key.waypointIndex];
        }
        void OnDestroy()
        {
            var values = this.roadsMapForJob.GetKeyValueArrays(Allocator.Temp).Values;
            for (int i = 0; i < values.Length; i++)
            {
                for (int j = 0; j < values[i].lanes.Value.arrays.Length; j++)
                {
                    values[i].lanes.Value.arrays[j].waypoints.Dispose();
                }
                values[i].lanes.Dispose();
            }
            this.roadsMapForJob.Dispose();
        }


    }

    #region waypointStructTypes
    [StructLayout(LayoutKind.Sequential)]
    public struct WaypointKey
    {
        public int roadId;
        public int laneIndex;
        public int waypointIndex;
        public override string ToString()
        {
            return "(roadId:" + roadId + ", laneIndex:" + laneIndex + ", waypointIndex:" + waypointIndex+")";
        }
        public static bool operator ==(WaypointKey c1, WaypointKey c2)
        {
            return c1.roadId == c2.roadId && c1.laneIndex == c2.laneIndex && c1.waypointIndex == c2.waypointIndex;
        }

        public static bool operator !=(WaypointKey c1, WaypointKey c2)
        {
            return !(c1 == c2);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Waypoint
    {
        public WaypointKey currentKey;
        public float3 position;
        public BlobAssetReference<MyBlobAsset<WaypointKey>> nextRoadWaypointKeys;
        public static bool operator ==(Waypoint c1, Waypoint c2)
        {
            return c1.currentKey == c2.currentKey;
        }

        public static bool operator !=(Waypoint c1, Waypoint c2)
        {
            return !(c1 == c2);
        }
    } 
    
    public struct Lane
    {
        public BlobAssetReference<MyBlobAsset<Waypoint>> waypoints;
        public static bool operator ==(Lane c1, Lane c2)
        {
            return c1.waypoints.Value.arrays[0] == c2.waypoints.Value.arrays[0];
        }

        public static bool operator !=(Lane c1, Lane c2)
        {
            return !(c1==c2);
        }

        public static bool operator &(Lane c1, Lane c2)
        {
            ref var lane1_waypoint0 = ref c1.waypoints.Value.arrays[0];
            ref var lane2_waypoint0 = ref c2.waypoints.Value.arrays[0];
            if (lane1_waypoint0.currentKey.roadId != lane2_waypoint0.currentKey.roadId)
                return false;
            ref var lane1_waypoint1 = ref c1.waypoints.Value.arrays[1];
            ref var lane2_waypoint1 = ref c2.waypoints.Value.arrays[1];
            return MathUltilities.Ultilities_Burst.Angle2(lane1_waypoint0.position - lane1_waypoint1.position, 
                                                                        lane2_waypoint0.position - lane2_waypoint1.position) < 90 ;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Road
    {
        public int roadId;
        public RoadType type;
        public BlobAssetReference<MyBlobAsset<Lane>> lanes;

        public static bool operator ==(Road c1, Road c2)
        {
            return c1.roadId == c2.roadId;
        }

        public static bool operator !=(Road c1, Road c2)
        {
            return !(c1 == c2);
        }
    }
    public enum RoadType
    {
        MAIN_ROAD = 1,
        CONNECTOR_ROAD = 0,
    }
    #endregion
}
