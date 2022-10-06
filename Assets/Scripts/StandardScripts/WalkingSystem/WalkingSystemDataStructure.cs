using System;
using Unity.Mathematics;
using System.Runtime.InteropServices;

namespace WalkingPath
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MyGraphNode
    {
        public int internalIndex;
        public uint graphArea;
        public MapArea mapArea;
        public float3 vertex0;
        public float3 vertex1;
        public float3 vertex2;
        public Circle inCircle;

        public override string ToString()
        {
            return (vertex0) + " " + (vertex1) + " " + (vertex2) + mapArea.ToString() + " " + internalIndex;
        }

        public float3 getCenter()
        {
            return inCircle.center;
        }

        public float getRadius()
        {
            return inCircle.radius;
        }

    }

    public struct MyConnection
    {
        public int destinationIndex;
        public uint cost;
        public override string ToString()
        {
            return "index " + destinationIndex + " cost " + cost;
        }
    }

    [Serializable]
    public enum MapArea
    {
        ROAD = 0,
        ROAD_SIDEWALK = 1,
        ROAD_INTERSECTION = 2,
        ROAD_SIDEWALK_INTERSECTION = 3,
        LANDSCAPE = 4,
        FOOTPATH = 5,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Sidewalk
    {
        public int colliderId;
        public float3 positionInner;
        public float3 positionOuter;

        public static bool isOnSameSideWalk(in Sidewalk a, in Sidewalk b)
        {
            return a.colliderId == b.colliderId;
        }

        public override string ToString()
        {
            return "sidewalk (" + positionInner + ", " + positionOuter + ") id:" + colliderId;
        }

        public static bool operator ==(in Sidewalk c1, in Sidewalk c2)
        {
            return c1.colliderId == c2.colliderId && math.distancesq(c1.positionInner, c2.positionInner) < .1f && math.distancesq(c1.positionOuter, c2.positionOuter) < .1f;
        }
        public static bool operator !=(in Sidewalk c1, in Sidewalk c2)
        {
            return !(c1 == c2);
        }
    }
}

//bonus Struct
public struct Circle
{
    public float3 center;
    public float radius;
    public Circle(in float3 center, in float radius)
    {
        this.center = center;
        this.radius = radius;
    }

    public override string ToString()
    {
        return "(c: " + center + "r: " + radius + ")";
    }
}