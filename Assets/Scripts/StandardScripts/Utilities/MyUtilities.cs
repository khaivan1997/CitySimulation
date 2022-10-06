
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;


using Assert = UnityEngine.Assertions.Assert;
public class MyUtilities
{
    public static float maxRayCastDistance = Mathf.Infinity;

    public static bool isPathSystemReady()
    {
        return WalkingPath.WalkingSystem.instance != null && VehicleNS.TrafficSystem.instance != null;
    }
    public static List<GameObject> getGameObjectsAtPoint(Vector3 point, string[] masks)
    {
        List<GameObject> res = new List<GameObject>();
        int layer = LayerMask.GetMask(masks);
        RaycastHit[] objects = Physics.RaycastAll(point, Vector3.up, maxRayCastDistance, layer, QueryTriggerInteraction.Ignore);
        foreach (var x in objects)
            res.Add(x.collider.gameObject);
        return res;
    }

    public static GameObject getGameObjectAtPoint(Vector3 point, string mask)
    {

        int layer = LayerMask.GetMask(mask);
        RaycastHit hit;
        Physics.Raycast(point, Vector3.up, out hit, maxRayCastDistance, layer, QueryTriggerInteraction.Ignore);

        return hit.collider.gameObject;
    }

    public static GameObject findClosetWayPoint(Vector3 point, float radius)
    {
        Collider[] colliders = Physics.OverlapSphere(point, radius, LayerMask.GetMask("roadwaypoints", "connectorwaypoints"), QueryTriggerInteraction.Collide);
        float minimum = -1f;
        GameObject waypoint = null;
        foreach (var col in colliders)
        {
            Vector3 distanceVector = col.transform.position - point;
            distanceVector.y = 0;
            float distance = distanceVector.magnitude;
            if (minimum == -1f || distance < minimum)
            {
                minimum = distance;
                waypoint = col.gameObject;
            }
            if (Mathf.Approximately(distance, 0))
                break;

        }
        return waypoint;
    }

    public static BlobAssetReference<MyBlobAsset<T>> createReference<T>(NativeArray<T> data) where T : unmanaged
    {
        using (BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp))
        {
            ref MyBlobAsset<T> blobAsset = ref blobBuilder.ConstructRoot<MyBlobAsset<T>>();
            BlobBuilderArray<T> arrayKey = blobBuilder.Allocate(ref blobAsset.arrays, data.Length);
            for (int i = 0; i < data.Length; i++)
                arrayKey[i] = data[i];
            BlobAssetReference<MyBlobAsset<T>> reference = blobBuilder.CreateBlobAssetReference<MyBlobAsset<T>>(Allocator.Persistent);
            return reference;
        };
    }

    #region MemoryUtility
    /*
     < credits roll >

    https://forum.unity.com/threads/terraindata-api-nativearray.662620/#post-4442287

    https://gist.github.com/LotteMakesStuff/6198f966e414a88d1337b0360cb891f5

    < credits roll >*/
    public unsafe static void MemCpy<SRC, DST>(SRC[] src, DST[] dst)
     where SRC : struct
     where DST : struct
    {
        int srcSize = src.Length * UnsafeUtility.SizeOf<SRC>();
        int dstSize = dst.Length * UnsafeUtility.SizeOf<DST>();
        Assert.AreEqual(srcSize, dstSize, $"{nameof(srcSize)}:{srcSize} and {nameof(dstSize)}:{dstSize} must be equal.");
        void* srcPtr = UnsafeUtility.PinGCArrayAndGetDataAddress(src, out ulong srcHandle);
        void* dstPtr = UnsafeUtility.PinGCArrayAndGetDataAddress(dst, out ulong dstHandle);
        UnsafeUtility.MemCpy(destination: dstPtr, source: srcPtr, size: srcSize);
        UnsafeUtility.ReleaseGCObject(srcHandle);
        UnsafeUtility.ReleaseGCObject(dstHandle);
    }

    public unsafe static void MemCpy<SRC, DST>(NativeArray<SRC> src, DST[] dst)
        where SRC : struct
        where DST : struct
    {
        int srcSize = src.Length * UnsafeUtility.SizeOf<SRC>();
        int dstSize = dst.Length * UnsafeUtility.SizeOf<DST>();
        Assert.AreEqual(srcSize, dstSize, $"{nameof(srcSize)}:{srcSize} and {nameof(dstSize)}:{dstSize} must be equal.");
        void* srcPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(src);
        void* dstPtr = UnsafeUtility.PinGCArrayAndGetDataAddress(dst, out ulong handle);
        UnsafeUtility.MemCpy(destination: dstPtr, source: srcPtr, size: srcSize);
        UnsafeUtility.ReleaseGCObject(handle);
    }

    public unsafe static NativeArray<float3> GetNativeArrayFloat3fromVector3(Vector3[] source, Allocator alloc)
    {
        NativeArray<float3> verts = new NativeArray<float3>(source.Length, alloc,
            NativeArrayOptions.UninitializedMemory);

        fixed (void* vertexBufferPointer = source)
        {
            UnsafeUtility.MemCpy(NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(verts),
                vertexBufferPointer, source.Length * (long)UnsafeUtility.SizeOf<float3>());
        }

        return verts;
    }
    #endregion

    public static GameObject[] FindGameObjectsOfTypeWithLayer<T>(LayerMask mask) where T : Component
    {
        List<int> layerList = convertMaskToLayers(mask);
        foreach (int i in layerList)
            Debug.Log("Find With Layer " + LayerMask.LayerToName(i));
        var goArray = GameObject.FindObjectsOfType<T>();
        var goList = new List<GameObject>();
        for (var i = 0; i < goArray.Length; i++)
        {
            if (layerList.Contains(goArray[i].gameObject.layer))
                goList.Add(goArray[i].gameObject);
        }
        if (goList.Count == 0)
            return null;
        return goList.ToArray();
    }

    public static List<int> convertMaskToLayers(LayerMask mask)
    {
        List<int> layerList = new List<int>(3);
        for (int i = 0; i < 32; i++)
        {
            if ((uint)(mask & 1 << i) > 0)
                layerList.Add(i);
        }
        return layerList;
    }
}

#region NativeCollectionExtension
public static class NativeListExtensions
{
    /// <summary>
    /// Reverses a <see cref="NativeList{T}"/>.
    /// </summary>
    /// <typeparam name="T"><see cref="NativeList{T}"/>.</typeparam>
    /// <param name="list">The <see cref="NativeList{T}"/> to reverse.</param>
    public static void Reverse<T>(this NativeList<T> list)
        where T : struct
    {
        var length = list.Length;
        var index1 = 0;

        for (var index2 = length - 1; index1 < index2; --index2)
        {
            var obj = list[index1];
            list[index1] = list[index2];
            list[index2] = obj;
            ++index1;
        }
    }

    /// <summary>
    /// Insert an element into a list.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="list">The list.</param>
    /// <param name="item">The element.</param>
    /// <param name="index">The index.</param>
    public static unsafe void Insert<T>(this NativeList<T> list, T item, int index)
        where T : struct
    {
        if (list.Length == list.Capacity - 1)
        {
            list.Capacity *= 2;
        }

        // Inserting at end same as an add
        if (index == list.Length)
        {
            list.Add(item);
            return;
        }

        if (index < 0 || index > list.Length)
        {
            throw new IndexOutOfRangeException();
        }

        // add a default value to end to list to increase length by 1
        list.Add(default);

        int elemSize = UnsafeUtility.SizeOf<T>();
        byte* basePtr = (byte*)list.GetUnsafePtr();

        var from = (index * elemSize) + basePtr;
        var to = (elemSize * (index + 1)) + basePtr;
        var size = elemSize * (list.Length - index - 1); // -1 because we added an extra fake element

        UnsafeUtility.MemMove(to, from, size);

        list[index] = item;
    }

    /// <summary>
    /// Remove an element from a <see cref="NativeList{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of NativeList.</typeparam>
    /// <typeparam name="TI">The type of element.</typeparam>
    /// <param name="list">The NativeList.</param>
    /// <param name="element">The element.</param>
    /// <returns>True if removed, else false.</returns>
    public static bool Remove<T, TI>(this NativeList<T> list, TI element)
        where T : struct, IEquatable<TI>
        where TI : struct
    {
        var index = list.IndexOf(element);
        if (index < 0)
        {
            return false;
        }

        list.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Remove an element from a <see cref="NativeList{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="list">The list to remove from.</param>
    /// <param name="index">The index to remove.</param>
    public static void RemoveAt<T>(this NativeList<T> list, int index)
        where T : struct
    {
        list.RemoveRange(index, 1);
    }

    /// <summary>
    /// Removes a range of elements from a <see cref="NativeList{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="list">The list to remove from.</param>
    /// <param name="index">The index to remove.</param>
    /// <param name="count">Number of elements to remove.</param>
    public static unsafe void RemoveRange<T>(this NativeList<T> list, int index, int count)
        where T : struct
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= (uint)list.Length)
        {
            throw new IndexOutOfRangeException(
                $"Index {index} is out of range in NativeList of '{list.Length}' Length.");
        }
#endif

        int elemSize = UnsafeUtility.SizeOf<T>();
        byte* basePtr = (byte*)list.GetUnsafePtr();

        UnsafeUtility.MemMove(basePtr + (index * elemSize), basePtr + ((index + count) * elemSize), elemSize * (list.Length - count - index));

        // No easy way to change length so we just loop this unfortunately.
        for (var i = 0; i < count; i++)
        {
            list.RemoveAtSwapBack(list.Length - 1);
        }
    }

    /// <summary>
    /// Resizes a <see cref="NativeList{T}"/> and then clears the memory.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="buffer">The <see cref="NativeList{T}"/> to resize.</param>
    /// <param name="length">Size to resize to.</param>
    public static unsafe void ResizeInitialized<T>(this NativeList<T> buffer, int length)
        where T : struct
    {
        buffer.ResizeUninitialized(length);
        UnsafeUtility.MemClear(buffer.GetUnsafePtr(), length * UnsafeUtility.SizeOf<T>());
    }

    /// <summary>
    /// Resizes a <see cref="NativeList{T}"/> and then sets all the bits to 1.
    /// For an integer array this is the same as setting the entire thing to -1.
    /// </summary>
    /// <param name="buffer">The <see cref="NativeList{T}"/> to resize.</param>
    /// <param name="length">Size to resize to.</param>
    public static void ResizeInitializeNegativeOne(this NativeList<int> buffer, int length)
    {
        buffer.ResizeUninitialized(length);

#if UNITY_2019_3_OR_NEWER
        unsafe
        {
            UnsafeUtility.MemSet(buffer.GetUnsafePtr(), byte.MaxValue, length * UnsafeUtility.SizeOf<int>());
        }
#else
            for (var i = 0; i < length; i++)
            {
                buffer[i] = -1;
            }
#endif
    }
}
#endregion

public struct MyBlobAsset<T> where T : unmanaged
{
    public BlobArray<T> arrays;
}


public class EnumUtil<T> where T: Enum
{
    public static bool ConsistsInt(int k)
    {
        return Array.Exists<int>(Enum.GetValues(typeof(T)).Cast<int>().ToArray<int>(), v => v == k);
    }
}