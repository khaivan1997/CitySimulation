
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

public static class RayCastHitConvert
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RaycastHitPublic
    {
        public Vector3 m_Point;
        public Vector3 m_Normal;
        public int m_FaceID;
        public float m_Distance;
        public Vector2 m_UV;
        public int m_ColliderID;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int GetColliderID(RaycastHit hit)
    {
        return UnsafeUtility.As<RaycastHit, RaycastHitPublic>(ref hit).m_ColliderID;
    }
}
