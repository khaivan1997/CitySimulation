using UnityEngine;
using Pathfinding;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using System.Collections.Generic;
using Unity.Entities;
using MathUltilities;

namespace WalkingPath
{
    [BurstCompile(CompileSynchronously = true)]
    public struct CalculateWaypointsWalkingJob : IJob
    {
        [ReadOnly]
        public NativeArray<MyGraphNode> graphNodes;
        [ReadOnly]
        public NativeMultiHashMap<int, MyConnection> graphConnections;
        [ReadOnly]
        public NativeMultiHashMap<int, Sidewalk> graphNodesWalkableArea;
        [ReadOnly]
        public int startPoint_index;
        [ReadOnly]
        public int endPoint_index;
        [ReadOnly]
        public float3 startPoint;
        [ReadOnly]
        public float3 endPoint;
        [ReadOnly]
        public uint tolerantDistance;
        //result
        [WriteOnly]
        public NativeList<float3> waypoints;
        [WriteOnly]
        public NativeList<int> nodesIndex;

        public void Execute()
        {
            this.DoDjikstra();
        }

        private void DoDjikstra()
        {
            int length = graphNodes.Length;
            NativeBitArray visited = new NativeBitArray(length, Allocator.Temp);
            NativeArray<int> pre = new NativeArray<int>(length, Allocator.Temp);
            NativeArray<uint> distances = new NativeArray<uint>(length, Allocator.Temp);
            Djikstra(startPoint_index, endPoint_index, length, visited, pre, distances);
            pre[startPoint_index] = 0;
            distances[startPoint_index] = 0;
            if (pre[endPoint_index] == 0)
                Debug.Log("no path");
            else
            {
                NativeList<int> res = new NativeList<int>(length, Allocator.Temp);
                int node_index = endPoint_index;
                do
                {
                    res.Add(node_index);
                    node_index = pre[node_index];
                } while (node_index > 0);

                var firstNodeIndex = res.Length - 1;
                var node = graphNodes[res[firstNodeIndex]];
                float3 lastPoint = startPoint;
                waypoints.Add(lastPoint);
                nodesIndex.Add(res[firstNodeIndex]);
                if (!Ultilities_Burst.isPointInsideTriangleXZ(node.vertex0, node.vertex1, node.vertex2, startPoint))
                    lastPoint = node.getCenter();
                else firstNodeIndex--;


                var endNodeIndex = 0;
                node = graphNodes[res[endNodeIndex]];
                if (Ultilities_Burst.isPointInsideTriangleXZ(node.vertex0, node.vertex1, node.vertex2, endPoint))
                    endNodeIndex++;
                for (int i = firstNodeIndex; i >= endNodeIndex; i--)
                {
                    node_index = res[i];
                    nodesIndex.Add(node_index);
                    float3 nextPoint = endPoint;
                    if (i > endNodeIndex) nextPoint = graphNodes[res[i - 1]].getCenter();
                    if (graphNodesWalkableArea.ContainsKey(node_index))
                    {
                        var it = graphNodesWalkableArea.GetValuesForKey(node_index);
                        Sidewalk currentSidewalk = new Sidewalk { colliderId = -1 };
                        while (it.MoveNext())
                        {
                            var hit = it.Current;
                            if (math.distancesq(hit.positionInner, hit.positionOuter) < .1f)
                                continue;
                            float3 closetPointOnSidewalk = VectorMath.ClosestPointOnLine(hit.positionOuter, hit.positionInner, lastPoint);
                            var walkingDirection = nextPoint - lastPoint;
                            var sidewalkiDirection = closetPointOnSidewalk - lastPoint;
                            var parallelAngle = 15;
                            if (Ultilities_Burst.Angle2(walkingDirection, sidewalkiDirection) > 90
                                || Ultilities_Burst.Angle2(walkingDirection, hit.positionInner - hit.positionOuter) < parallelAngle
                                || Ultilities_Burst.Angle2(walkingDirection, hit.positionInner - hit.positionOuter) > (180f - parallelAngle))
                                continue;
                            if (currentSidewalk.colliderId <= 0)
                                currentSidewalk = hit;
                            else if (currentSidewalk.colliderId > 0)
                            {
                                float3 lastSidewalkiDirection = (float3)VectorMath.ClosestPointOnLine(currentSidewalk.positionOuter, currentSidewalk.positionInner, lastPoint) - lastPoint;
                                if (Ultilities_Burst.Angle2(walkingDirection, sidewalkiDirection) < Ultilities_Burst.Angle2(lastSidewalkiDirection, sidewalkiDirection))
                                    currentSidewalk = hit;
                            }
                        }
                        it.Dispose();
                        if (currentSidewalk.colliderId > 0)
                        {
                            if (Ultilities_Burst.f_xz(currentSidewalk.positionOuter, currentSidewalk.positionInner, lastPoint) > 0)
                                lastPoint = Ultilities.getMiddlePoint(currentSidewalk.positionOuter, currentSidewalk.positionInner, 1f, 3f);
                            else
                                lastPoint = Ultilities.getMiddlePoint(currentSidewalk.positionOuter, currentSidewalk.positionInner, 3f, 1f);
                        }
                        else
                        {
                            node = graphNodes[node_index];
                            Ultilities_Burst.ClosestPointOnTriangleXZ(node.vertex0, node.vertex1, node.vertex2, lastPoint, out lastPoint);
                        }
                    }
                    else
                    {
                        node = graphNodes[node_index];
                        Ultilities_Burst.ClosestPointOnTriangleXZ(node.vertex0, node.vertex1, node.vertex2, lastPoint, out lastPoint);
                    }
                    waypoints.Add(lastPoint);
                }
                res.Dispose();
            }
            visited.Dispose();
            pre.Dispose();
            distances.Dispose();
        }

        private void Djikstra(in int startPoint_index, in int endPoint_index, in int length,
            in NativeBitArray visited,
            in NativeArray<int> pre,
            in NativeArray<uint> distances)
        {
            visitNode(startPoint_index, visited, pre, distances);
            for (int i = 2; i < length; i++)
            {
                int toVisit = findNextNode(visited, distances, length);
                if (toVisit > 0)
                    visitNode(toVisit, visited, pre, distances);
                else return;
                if (visited.IsSet(endPoint_index))
                    return;
            }
        }

        private void visitNode(int node_index,
            in NativeBitArray visited,
            NativeArray<int> pre,
            NativeArray<uint> distances)
        {
            visited.Set(node_index, true);
            uint thisDistance = distances[node_index];
            if (graphConnections.ContainsKey(node_index))
            {
                var it = graphConnections.GetValuesForKey(node_index);
                while (it.MoveNext())
                {
                    var con = it.Current;
                    var neightborIndex = con.destinationIndex;
                    var distance = distances[neightborIndex];
                    uint currentDist = thisDistance + con.cost;
                    if (!visited.IsSet(neightborIndex) && (distance == 0 || currentDist < distance))
                    //|| (math.abs(currentDist - distance[neightborIndex]) < tolerantDistance && graphNodes[pre[neightborIndex]].mapArea != MapArea.FOOTPATH
                    //&& graphNodes[node_index].mapArea == MapArea.FOOTPATH))
                    {
                        distances[neightborIndex] = currentDist;
                        pre[neightborIndex] = node_index;
                    }
                };
                it.Dispose();
            }
        }

        private int findNextNode(in NativeBitArray visited, in NativeArray<uint> distances, in int length)
        {
            int minIndex = 0;
            for (int i = 1; i < length; i++)
            {
                var distance = distances[i];
                var minDistance = distances[minIndex];
                if (!visited.IsSet(i) && distance > 0 &&
                  (minIndex == 0 ||
                  distance < minDistance
                  || (math.abs(distance - minDistance) < tolerantDistance &&
                  graphNodes[minIndex].mapArea == MapArea.ROAD && graphNodes[i].mapArea != MapArea.ROAD)))
                    minIndex = i;
            } 
            return minIndex;
        }
    }


    [BurstCompile(CompileSynchronously = true)]
    public struct InterpolateSidewalkJob : IJobParallelForBatch
    {
        [ReadOnly]
        public NativeArray<MyGraphNode> graphNodes;
        [ReadOnly]
        public NativeMultiHashMap<int, MyConnection> graphConnections;
        [ReadOnly]
        [DeallocateOnJobCompletion]
        public NativeArray<int> keys;
        [ReadOnly]
        public NativeMultiHashMap<int, Sidewalk> graphNodes_sidewalks;

        [WriteOnly]
        public NativeMultiHashMap<int, Sidewalk>.ParallelWriter graphNodes_sidewalksWriter;

        public void Execute(int startIndex, int count)
        {
            int maxIteration = 3;
            int end = startIndex + count;
            for (int i = startIndex; i < end; i++)
            {
                var nodeIndex = keys[i];
                if (graphNodes[nodeIndex].mapArea == MapArea.ROAD_SIDEWALK_INTERSECTION)
                    continue;
                var node = graphNodes[nodeIndex];
                NativeList<int> neighbors = new NativeList<int>(3 * maxIteration, Allocator.Temp);

                var it = graphNodes_sidewalks.GetValuesForKey(nodeIndex);
                while (it.MoveNext())
                {
                    var s = it.Current;
                    if (math.distancesq(s.positionInner, s.positionOuter) > .1f)
                    {
                        neighbors.Clear();
                        GatherNeighbors(nodeIndex, maxIteration, neighbors);

                        float3 finalPoint = s.positionInner;
                        for (int j = 0; j < neighbors.Length; j++)
                        {
                            var neighborNode = graphNodes[neighbors[j]];
                            if (Ultilities_Burst.isLineIntersectTriangleXZ(s.positionInner, s.positionOuter,
                                neighborNode.vertex0, neighborNode.vertex1, neighborNode.vertex2))
                            {
                                float3 point = VectorMath.ClosestPointOnLine(s.positionInner, s.positionOuter, neighborNode.getCenter());
                                if (math.distancesq(point, s.positionOuter) > math.distancesq(finalPoint, s.positionOuter)
                                    && Ultilities_Burst.Angle2(s.positionInner - s.positionOuter, point - s.positionOuter) < 90)
                                    finalPoint = point;
                            }
                            else
                                neighbors[j] = -1;
                        }
                        s.positionInner = finalPoint;
                        graphNodes_sidewalksWriter.Add(nodeIndex, s);
                        float3 sidewalkDirection = s.positionInner - s.positionOuter;
                        for (int j = 0; j < neighbors.Length; j++)
                        {
                            int index = neighbors[j];
                            if (index > 0)
                            {
                                var center = graphNodes[index].getCenter();
                                Sidewalk t = new Sidewalk
                                {
                                    positionInner = VectorMath.ClosestPointOnLine(center, center + sidewalkDirection, s.positionInner),
                                    positionOuter = VectorMath.ClosestPointOnLine(center, center + sidewalkDirection, s.positionOuter),
                                    colliderId = s.colliderId,
                                };
                                graphNodes_sidewalksWriter.Add(index, t);
                            }
                        }
                    }
                }
                it.Dispose();
                neighbors.Dispose();
            }
        }
        void GatherNeighbors(int nodeIndex, int maxIteration, in NativeList<int> nodes)
        {
            //Debug.Log($"gather for {nodeIndex}");
            NativeList<int> toVisit = new NativeList<int>(5, Allocator.Temp);
            NativeList<int> visited = new NativeList<int>(3 * maxIteration, Allocator.Temp);
            toVisit.Add(nodeIndex);
            for (int ite = 0; ite < maxIteration; ite++)
            {
                int count = toVisit.Length;
                while (count > 0)
                {
                    --count;
                    var index = toVisit[count];
                    toVisit.RemoveAtSwapBack(count);
                    visited.Add(index);
                    //Debug.Log($"visit at {ite} {index}");
                    if (graphConnections.ContainsKey(index))
                    {
                        var neightborIt = graphConnections.GetValuesForKey(index);
                        while (neightborIt.MoveNext())
                        {
                            var destinationIndex = neightborIt.Current.destinationIndex;
                            var mapArea = graphNodes[destinationIndex].mapArea;
                            if (!visited.Contains(destinationIndex) && mapArea != MapArea.ROAD
                                && mapArea != MapArea.ROAD_INTERSECTION)
                            {
                                toVisit.Add(destinationIndex);
                                if (graphNodes[destinationIndex].mapArea == MapArea.ROAD_SIDEWALK)
                                {
                                    Sidewalk s;
                                    NativeMultiHashMapIterator<int> it;
                                    graphNodes_sidewalks.TryGetFirstValue(destinationIndex, out s, out it);
                                    //Debug.Log($"    sidewalk: {s.ToString()}");
                                    if (s.colliderId > 0 && math.distancesq(s.positionInner, s.positionOuter) < .1f)
                                        nodes.Add(destinationIndex);
                                }
                                else if (mapArea != MapArea.ROAD_INTERSECTION)
                                    nodes.Add(destinationIndex);
                            }
                        }
                    }
                }
            }
            toVisit.Dispose();
            visited.Dispose();
        }
    }

    //Job to find sidewalk intersection with navmesh node
    [BurstCompile(CompileSynchronously = true)]
    public struct FindSideWalkIntersectionJob : IJobParallelForBatch
    {
        [ReadOnly]
        public float3 vertex0, vertex1, vertex2;
        [ReadOnly]
        [DeallocateOnJobCompletion]
        public NativeArray<float3> vertices;
        [ReadOnly]
        [DeallocateOnJobCompletion]
        public NativeArray<int> triangles;
        [ReadOnly]
        public Matrix4x4 localToWorldMatrix;
        //result
        [WriteOnly]
        public NativeQueue<float3>.ParallelWriter subResult;
        public void Execute(int startIndex, int count)
        {
            Circle inCircle = new Circle();
            Ultilities_Burst.getIncircle(vertex0, vertex1, vertex2, out inCircle);
            inCircle.radius = WalkingSystem.PAVEMENT_WIDTH;
            int end = startIndex + count;
            for (int i = startIndex; i < end; i++)
            {
                float3 v0 = localToWorldMatrix.MultiplyPoint3x4(vertices[triangles[i * 3]]);
                float3 v1 = localToWorldMatrix.MultiplyPoint3x4(vertices[triangles[i * 3 + 1]]);
                float3 v2 = localToWorldMatrix.MultiplyPoint3x4(vertices[triangles[i * 3 + 2]]);
                float3 center = Ultilities.getTriangleCenter(v0, v1, v2);
                if (math.distance(center, inCircle.center) > inCircle.radius)
                    continue;
                if (Ultilities_Burst.isTriangleIntersectCircleXZ(v0, v1, v2, inCircle))
                {
                    subResult.Enqueue(v0);
                    subResult.Enqueue(v1);
                    subResult.Enqueue(v2);
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct FindSideWalkPointJob : IJob
    {
        [ReadOnly]
        public float3 vertex0, vertex1, vertex2;
        [ReadOnly]
        public int resultIndex;
        [ReadOnly]
        public int colliderId;

        public NativeQueue<float3> subResult;
        [WriteOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeMultiHashMap<int, Sidewalk>.ParallelWriter graphNodes_sidewalks;
        public void Execute()
        {
            Circle c = new Circle();
            Ultilities_Burst.getIncircle(vertex0, vertex1, vertex2, out c);
            int count = subResult.Count / 3;
            NativeList<float3> outerPoints = new NativeList<float3>(count / 3, Allocator.Temp);
            NativeList<float3> innerTri = new NativeList<float3>(count, Allocator.Temp);
            for (int i = 0; i < count; i++)
            {
                float3 v0 = subResult.Dequeue();
                float3 v1 = subResult.Dequeue();
                float3 v2 = subResult.Dequeue();
                if (Ultilities_Burst.isTriangleWalkable(v0, v1, v2))
                {
                    innerTri.Add(v0);
                    innerTri.Add(v1);
                    innerTri.Add(v2);
                }
                else
                    outerPoints.Add(Polygon.ClosestPointOnTriangle(v0, v1, v2, c.center));

            }

            float3 outerPoint = float3.zero;
            float3 innerPoint = c.center;
            for (int i = 0; i < outerPoints.Length; i++)
                if (math.all(outerPoint == float3.zero) || math.distance(outerPoint, c.center) > math.distance(outerPoints[i], c.center))
                    outerPoint = outerPoints[i];


            if (math.all(outerPoint == float3.zero))
            {
                graphNodes_sidewalks.Add(resultIndex, new Sidewalk
                {
                    positionInner = c.center,
                    positionOuter = c.center,
                    colliderId = colliderId,
                });
                return;
            }
            else
            {
                Ultilities_Burst.ClosestPointOnPlane(vertex0, vertex1, vertex2, outerPoint, out outerPoint);
                float3 direction = (outerPoint - c.center);
                float maxAngle = 0;
                for (int i = 0; i < innerTri.Length / 3; i++)
                {
                    float3 v0 = innerTri[i * 3];
                    float3 v1 = innerTri[i * 3 + 1];
                    float3 v2 = innerTri[i * 3 + 2];
                    if (Ultilities_Burst.isLineIntersectTriangleXZ(c.center, outerPoint, v0, v1, v2))
                    {
                        float angle = Ultilities_Burst.Angle2(direction, v0 - c.center);
                        if (angle > maxAngle)
                        {
                            innerPoint = v0;
                            maxAngle = angle;
                        }

                        angle = Ultilities_Burst.Angle2(direction, v1 - c.center);
                        if (angle > maxAngle)
                        {
                            innerPoint = v1;
                            maxAngle = angle;
                        }

                        angle = Ultilities_Burst.Angle2(direction, v2 - c.center);
                        if (angle > maxAngle)
                        {
                            innerPoint = v2;
                            maxAngle = angle;
                        }
                    }
                }
                innerPoint = VectorMath.ClosestPointOnLine(outerPoint, c.center, innerPoint);
            }

            graphNodes_sidewalks.Add(resultIndex, new Sidewalk
            {
                positionInner = innerPoint,
                positionOuter = outerPoint,
                colliderId = colliderId,
            });

            outerPoints.Dispose();
            innerTri.Dispose();
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct CalculateWaypointsWalkingJobWithHeap : IJob
    {
        [ReadOnly]
        public NativeArray<MyGraphNode> graphNodes;
        [ReadOnly]
        public NativeMultiHashMap<int, MyConnection> graphConnections;
        [ReadOnly]
        public NativeMultiHashMap<int, Sidewalk> graphNodesWalkableArea;
        [ReadOnly]
        public int startPoint_index;
        [ReadOnly]
        public int endPoint_index;
        [ReadOnly]
        public float3 startPoint;
        [ReadOnly]
        public float3 endPoint;
        [ReadOnly]
        public uint tolerantDistance;
        //result
        
        [WriteOnly]
        [NativeDisableContainerSafetyRestriction]
        [NativeDisableParallelForRestriction]
        public DynamicBuffer<WalkingPathBuffer> paths;

        public void Execute()
        {
            this.DoDjikstra();
        }

        private void DoDjikstra()
        {
            int length = graphNodes.Length;
            NativeBitArray visited = new NativeBitArray(length, Allocator.Temp);
            NativeArray<int> pre = new NativeArray<int>(length, Allocator.Temp);
            MyNativeHeap<DistanceStruct, DistanceComparator> distances = new MyNativeHeap<DistanceStruct, DistanceComparator>(length, Allocator.Temp);
            NativeArray<int> distanceKeys = new NativeArray<int>(length, Allocator.Temp);
            for(int i = 0; i < length; i++)
            {
                distanceKeys[i] = -1;
            }
            Djikstra(startPoint_index, endPoint_index, length, visited, pre, distances, distanceKeys);
            pre[startPoint_index] = 0;
            if (pre[endPoint_index] == 0)
                Debug.Log("no path");
            else
            {
                NativeList<int> res = new NativeList<int>(length, Allocator.Temp);
                int node_index = endPoint_index;
                do
                {
                    res.Add(node_index);
                    node_index = pre[node_index];
                } while (node_index > 0);

                var firstNodeIndex = res.Length - 1;
                var node = graphNodes[res[firstNodeIndex]];
                float3 lastPoint = startPoint;
                paths.Add(new WalkingPathBuffer{
                    position = lastPoint,
                    node_index = startPoint_index,
                });

                if (!Ultilities_Burst.isPointInsideTriangleXZ(node.vertex0, node.vertex1, node.vertex2, startPoint))
                    lastPoint = node.getCenter();
                else firstNodeIndex--;

                var endNodeIndex = 0;
                node = graphNodes[res[endNodeIndex]];
                if (Ultilities_Burst.isPointInsideTriangleXZ(node.vertex0, node.vertex1, node.vertex2, endPoint))
                    endNodeIndex++;
                for (int i = firstNodeIndex; i >= endNodeIndex; i--)
                {
                    node_index = res[i];
                    float3 nextPoint = endPoint;
                    if (i > endNodeIndex) nextPoint = graphNodes[res[i - 1]].getCenter();
                    if (graphNodesWalkableArea.ContainsKey(node_index))
                    {
                        var it = graphNodesWalkableArea.GetValuesForKey(node_index);
                        Sidewalk currentSidewalk = new Sidewalk { colliderId = -1 };
                        while (it.MoveNext())
                        {
                            var hit = it.Current;
                            if (math.distancesq(hit.positionInner, hit.positionOuter) < .1f)
                                continue;
                            float3 closetPointOnSidewalk = VectorMath.ClosestPointOnLine(hit.positionOuter, hit.positionInner, lastPoint);
                            var walkingDirection = nextPoint - lastPoint;
                            var sidewalkiDirection = closetPointOnSidewalk - lastPoint;
                            var parallelAngle = 15;
                            if (Ultilities_Burst.Angle2(walkingDirection, sidewalkiDirection) > 90
                                || Ultilities_Burst.Angle2(walkingDirection, hit.positionInner - hit.positionOuter) < parallelAngle
                                || Ultilities_Burst.Angle2(walkingDirection, hit.positionInner - hit.positionOuter) > (180f - parallelAngle))
                                continue;
                            if (currentSidewalk.colliderId <= 0)
                                currentSidewalk = hit;
                            else if (currentSidewalk.colliderId > 0)
                            {
                                float3 lastSidewalkiDirection = (float3)VectorMath.ClosestPointOnLine(currentSidewalk.positionOuter, currentSidewalk.positionInner, lastPoint) - lastPoint;
                                if (Ultilities_Burst.Angle2(walkingDirection, sidewalkiDirection) < Ultilities_Burst.Angle2(lastSidewalkiDirection, sidewalkiDirection))
                                    currentSidewalk = hit;
                            }
                        }
                        it.Dispose();
                        if (currentSidewalk.colliderId > 0)
                        {
                            if (Ultilities_Burst.f_xz(currentSidewalk.positionOuter, currentSidewalk.positionInner, lastPoint) > 0)
                                lastPoint = Ultilities.getMiddlePoint(currentSidewalk.positionOuter, currentSidewalk.positionInner, 1f, 3f);
                            else
                                lastPoint = Ultilities.getMiddlePoint(currentSidewalk.positionOuter, currentSidewalk.positionInner, 3f, 1f);
                        }
                        else
                        {
                            node = graphNodes[node_index];
                            Ultilities_Burst.ClosestPointOnTriangleXZ(node.vertex0, node.vertex1, node.vertex2, lastPoint, out lastPoint);
                        }
                    }
                    else
                    {
                        node = graphNodes[node_index];
                        Ultilities_Burst.ClosestPointOnTriangleXZ(node.vertex0, node.vertex1, node.vertex2, lastPoint, out lastPoint);
                    }
                    paths.Add(new WalkingPathBuffer
                    {
                        position = lastPoint,
                        node_index = node_index
                    });
                }
                res.Dispose();
            }
            paths.Add(new WalkingPathBuffer
            {
                position = endPoint,
                node_index = endPoint_index
            });
            visited.Dispose();
            pre.Dispose();
            distances.Dispose();
            distanceKeys.Dispose();
        }

        private void Djikstra(in int startPoint_index, in int endPoint_index, in int length,
            in NativeBitArray visited,
            in NativeArray<int> pre,
            in MyNativeHeap<DistanceStruct, DistanceComparator> distances,
            in NativeArray<int> distanceKey)
        {
            visitNode(startPoint_index, visited, pre, distances, distanceKey);
            for (int i = 2; i < length; i++)
            {
                int toVisit = findNextNode(visited, distances, length);
                if (toVisit > 0)
                    visitNode(toVisit, visited, pre, distances, distanceKey);
                else return;
                if (visited.IsSet(endPoint_index))
                    return;
            }
        }

        private void visitNode(int node_index,
            in NativeBitArray visited,
            NativeArray<int> pre,
            in MyNativeHeap<DistanceStruct, DistanceComparator> distances,
            NativeArray<int> distanceKey)
        {
            visited.Set(node_index, true);
            var nodeKey = distanceKey[node_index];
            uint thisDistance = 0;
            if (nodeKey >= 0)
                thisDistance = distances.Remove(nodeKey).distance;
            if (graphConnections.ContainsKey(node_index))
            {
                var it = graphConnections.GetValuesForKey(node_index);
                while (it.MoveNext())
                {
                    var con = it.Current;
                    var neightborIndex = con.destinationIndex;
                    uint distance = 0;
                    int neightborKey = distanceKey[neightborIndex];
                    if (neightborKey >= 0)
                        distance = distances.Peek(neightborKey).distance;
                    uint currentDist = thisDistance + con.cost;
                    if (!visited.IsSet(neightborIndex) && (distance == 0 || currentDist < distance))
                    //|| (math.abs(currentDist - distance[neightborIndex]) < tolerantDistance && graphNodes[pre[neightborIndex]].mapArea != MapArea.FOOTPATH
                    //&& graphNodes[node_index].mapArea == MapArea.FOOTPATH))
                    {
                        if(neightborKey >= 0)
                        {
                            distances.Remove(neightborKey);
                        }
                        distanceKey[neightborIndex] = distances.Insert(new DistanceStruct {distance = currentDist, node_index = neightborIndex});
                        pre[neightborIndex] = node_index;
                    }
                };
                it.Dispose();
            }
        }

        private int findNextNode(in NativeBitArray visited, in MyNativeHeap<DistanceStruct, DistanceComparator> distances, in int length)
        {
            int minIndex = 0;
            if(distances.Length > 0)
            {
                minIndex = distances[0].node_index;
            }
            return minIndex;
        }
    }

    public struct DistanceStruct
    {
        public uint distance;
        public int node_index;
    }
    public struct DistanceComparator : IComparer<DistanceStruct>
    {
        public int Compare(DistanceStruct x, DistanceStruct y)
        {
            return (int)(x.distance - y.distance);
        }
    }
}
