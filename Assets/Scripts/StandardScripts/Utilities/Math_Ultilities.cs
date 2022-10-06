
namespace MathUltilities
{
    using UnityEngine;
    using Pathfinding;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;
    using Unity.Mathematics;

    public static class Ultilities
    {
        public static Vector3 getTriangleCenter(in Vector3 v0, in Vector3 v1, in Vector3 v2)
        {
            return (v0 + v1 + v2) / 3f;
        }
        public static float3 getMiddlePoint(in float3 v0, in float3 v1, float ratio0, float ratio1)
        {
            return (ratio0*v0 + ratio1*v1) / (ratio0+ratio1);
        }
        public static bool nodeHasSideWalk(in NativeArray<WalkingPath.MyGraphNode> graphNodes, in NativeMultiHashMap<int, WalkingPath.Sidewalk> sidewalks, int node_index, in WalkingPath.Sidewalk s)
        {
            if (sidewalks.ContainsKey(node_index))
            {
                var it = sidewalks.GetValuesForKey(node_index);
                while (it.MoveNext())
                {
                    var con = it.Current;
                    if (con.colliderId == s.colliderId)
                        return true;
                };
                it.Dispose();
            }
            return false;
        }
        public static bool nodesHasSameSidewalk(in NativeArray<WalkingPath.MyGraphNode> graphNodes, in NativeMultiHashMap<int, WalkingPath.Sidewalk> sidewalks, int node1_index, int node2_index)
        {
            if (sidewalks.ContainsKey(node1_index))
            {
                var it = sidewalks.GetValuesForKey(node1_index);
                while (it.MoveNext())
                {
                    var con = it.Current;
                    if (nodeHasSideWalk(graphNodes, sidewalks, node2_index, con))
                        return true;
                };
                it.Dispose();
            }
            return false;
        }
  
        public static float3 ClosestPointOnLine(in float3 linePoint1, in float3 linePoint2, in float3 point)
        {
            return VectorMath.ClosestPointOnLine(linePoint1, linePoint2, point);
        }

        public static void GatherNeighbors(in NativeArray<WalkingPath.MyGraphNode> graphNodes, in NativeMultiHashMap<int, WalkingPath.MyConnection> graphConnections,int nodeIndex, int maxIteration, in NativeList<int> nodes)
        {
                NativeQueue<int> toVisit = new NativeQueue<int>(Allocator.Temp);
                NativeList<int> visited = new NativeList<int>(3 * maxIteration, Allocator.Temp);
                toVisit.Enqueue(nodeIndex);
                for (int ite = 0; ite < maxIteration; ite++)
                {
                    int count = toVisit.Count;
                    while (count > 0)
                    {
                        count--;
                        var index = toVisit.Dequeue();
                        visited.Add(index);
                        //Debug.Log($"visit at {ite} {index}");
                        if (graphConnections.ContainsKey(index))
                        {
                            var neightborIt = graphConnections.GetValuesForKey(index);
                            while (neightborIt.MoveNext())
                            {
                                var destinationIndex = neightborIt.Current.destinationIndex;
                            //Debug.Log($"   neigbor: {destinationIndex} area: {graphNodes[destinationIndex].mapArea.ToString()}");
                            if (!visited.Contains(destinationIndex) && graphNodes[destinationIndex].mapArea != WalkingPath.MapArea.ROAD
                                && graphNodes[destinationIndex].mapArea != WalkingPath.MapArea.ROAD_INTERSECTION)
                            {
                                toVisit.Enqueue(destinationIndex);
                                nodes.Add(destinationIndex);
                            }
                                //{
                                

                            //    if (graphNodes[destinationIndex].mapArea == MapArea.ROAD_SIDEWALK)
                            //    {
                            //        Sidewalk s;
                            //        NativeMultiHashMapIterator<int> it;
                            //        graphNodes_sidewalks.TryGetFirstValue(destinationIndex, out s, out it);
                            //        Debug.Log($"    sidewalk: {s.ToString()}");
                            //        if (s.colliderId > 0 && math.distancesq(s.positionInner, s.positionOuter) < .1f)
                            //            nodes.Add(destinationIndex);
                            //    }
                            //    else if (graphNodes[destinationIndex].mapArea != MapArea.ROAD_INTERSECTION)


                        }
                        }
                    }
                }
                toVisit.Dispose();
                visited.Dispose();
                //Vector3 center = WalkingSystem_Ultilities.getTriangleCenter(graphNodes[nodeIndex].vertex0, graphNodes[nodeIndex].vertex1, graphNodes[nodeIndex].vertex2);
                //if(Vector3.Distance(center, new Vector3(417.3f, 187.9f, 408.5f)) < 5f){
                //    Debug.Log($"got at {nodeIndex}");
                //    foreach (var c in nodes)
                //        Debug.Log($"neighbor {c}");
                //}
            
        }

    }

    [BurstCompile]
    public static class Ultilities_Burst
    {
        [BurstCompile]
        public static bool isPointInsideTriangleXZ(in float3 v0, in float3 v1, in float3 v2, in float3 point)
        {
            if (Polygon.ContainsPointXZ(v0, v1, v2, point))
                return true;
            float3 closetPoint = Polygon.ClosestPointOnTriangleXZ(v0, v1, v2, point);
            float3 pointCopy = point;
            closetPoint.y = 0; pointCopy.y = 0;
            float distance = math.distance(closetPoint, point);
            if (distance < 1f)
                return true;
            return false;

        }

        [BurstCompile]
        public static bool isTriangleIntersectCircleXZ(in float3 vertex0, in float3 vertex1, in float3 vertex2, in Circle circle)
        {
            float3 v0 = vertex0, v1 = vertex1, v2 = vertex2;
            float3 center = circle.center;
            v0.y = 0f; v1.y = 0f; v2.y = 0f; center.y = 0;
            float radius = circle.radius;
            if (math.distance(v0, center) <= radius || math.distance(v1, center) <= radius || math.distance(v2, center) <= radius ||
                math.distance((v0 + v1) / 2f, center) <= radius || math.distance((v1 + v2) / 2f, center) <= radius || math.distance((v2 + v0) / 2f, center) <= radius)
                return true;
            Circle c = new Circle();
            getIncircle(v0, v1, v2, out c);
            if (math.distance(c.center, center) <= radius || math.distance(c.center, Ultilities.getTriangleCenter(v0,v1,v2))<=radius)
                return true;
            return false;
        }
        [BurstCompile]
        public static void ClosestPointOnTriangleXZ(in float3 v0, in float3 v1, in float3 v2, in float3 point, out float3 result)
        {
            result = Polygon.ClosestPointOnTriangleXZ(v0, v1, v2, point);

        }
        [BurstCompile]
        public static float HeuristicFunction_PureMath(in float3 position, in float3 target, int heuristicAlgo = 0)
        {
            if (heuristicAlgo == 0)
                return math.distance(position, target);
            if (heuristicAlgo > 0)
            {
                float heuristicScale = 1F;
                return (math.abs(target.x - position.x) + math.abs(target.y - position.y) + math.abs(target.z - position.z)) * heuristicScale;

            }

            return 0;
        }
        /*[BurstCompile]
        public static Vector3 getWalkPoint(in MyGraphNode n, in Sidewalk s)
        {
            var distances = new NativeArray<float>(3, Allocator.Temp);
            distances[0] = math.distance(n.vertex0, s.position);
            distances[1] = math.distance(n.vertex1, s.position);
            distances[2] = math.distance(n.vertex2, s.position);
            var minIndex = 0;
            for (int i = 1; i < distances.Length; i++)
            {
                if (distances[minIndex] > distances[i])
                    minIndex = i;
            }
            if (minIndex == 0)
                return (n.vertex0 + s.position) / 2f;
            if (minIndex == 1)
                return (n.vertex1 + s.position) / 2f;
            return (n.vertex2 + s.position) / 2f;
        }*/
        [BurstCompile]
        public static bool isTriangleWalkable(in float3 vertex0, in float3 vertex1, in float3 vertex2)
        {
            float maxSlope = WalkingPath.WalkingSystem.maxSlope;
            var triangleNormal = math.cross(vertex2 - vertex0, vertex1 - vertex0);
            float angle = Angle2(triangleNormal, math.up());
            if (angle > maxSlope && angle < 180f - maxSlope)
                return false;
            return true;
        }


        //credit https://forum.kerbalspaceprogram.com/index.php?/topic/164418-vector3angle-more-accurate-and-numerically-stable-at-small-angles-version/
        [BurstCompile]
        public static float Angle2(in float3 a, in float3 b)
        {
            var abm = a * b.magnitude();
            var bam = b * a.magnitude();
            return 2 * math.atan2((abm - bam).magnitude(), (abm + bam).magnitude()) * Mathf.Rad2Deg;
        }
        [BurstCompile]
        public static float magnitude(this in float3 v)
        {
            return math.distance(v,float3.zero);
        }

        [BurstCompile]
        public static void getIncircle(in float3 v0, in float3 v1, in float3 v2, out Circle res)
        {
            var c = math.distance(v0, v1);
            var a = math.distance(v1, v2);
            var b = math.distance(v2, v0);
            var p = a + b + c;
            var center = (a * v0 + b * v1 + c * v2) / p;
            p /= 2f;
            var radius = math.sqrt(p * (p - a) * (p - b) * (p - c)) / p;
            res = new Circle(center, radius);
        }

        [BurstCompile]
        public static void getOutcircle(in float3 v0, in float3 v1, in float3 v2, out Circle res)
        {
            float3 center = Ultilities.getTriangleCenter(v0, v1, v2);
            float radius = (math.distance(v0, center) + math.distance(v1, center) + math.distance(v2, center)) / 3f;//math.max(math.distance(v0, center), math.max(math.distance(v1, center), math.distance(v2, center)));
            res = new Circle(center, radius);
        }

        [BurstCompile]
        public static bool isLineIntersectTriangleXZ(in float3 linePoint1, in float3 linePoint2, in float3 v0, in float3 v1, in float3 v2)
        {
            float f_v0 = f_xz(linePoint1, linePoint2, v0),
                 f_v1 = f_xz(linePoint1, linePoint2, v1),
                  f_v2 = f_xz(linePoint1, linePoint2, v2);
            if ((f_v0 < 0 && f_v1 < 0 && f_v2 < 0) || (f_v0 > 0 && f_v1 > 0 && f_v2 > 0))
                return false;
            return true;
        }
        [BurstCompile]
        public static float f_xz(in float3 linePoint1, in float3 linePoint2, in float3 point)
        {
            float y1 = linePoint1.z, y2 = linePoint2.z,
                x1 = linePoint1.x, x2 = linePoint2.x,
                x = point.x, y = point.z;
            return (y2 - y1) * x + (x1 - x2) * y + (x2 * y1 - x1 * y2);
        }
        [BurstCompile]
        public static void ClosestPointOnPlane(in float3 vertex0, in float3 vertex1, in float3 vertex2, in float3 point, out float3 res)
        {
            /*float3 triangleNormal = math.cross(vertex2 - vertex0, vertex1 - vertex0);
            float t = (math.dot(triangleNormal, point) - math.dot(triangleNormal,vertex0)) / math.dot(triangleNormal, triangleNormal);
            res = vertex0 + t * triangleNormal;*/
            Plane p = new Plane(vertex0, vertex1, vertex2);
            res = p.ClosestPointOnPlane(point);
        }
    }

    
}


