using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using System.Diagnostics;

namespace Unity.Collections
{
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    public unsafe struct MyNativeHeap<T, C> : IDisposable where T : unmanaged where C : IComparer<T>
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct HeapNode
        {
            public T value;
            public int tableIndex;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct TableIndex
        {
            public int heapIndex;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            public int valid;
#endif
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HeapData
        {
            public int m_Length;
            public int m_Capacity;
            public void* m_Buffer;
            public TableIndex* m_Lookup_Table;
        }


        [NativeDisableUnsafePtrRestriction]
        private unsafe HeapData* m_Data;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        [NativeSetClassTypeToNullOnSchedule]
        internal DisposeSentinel m_DisposeSentinel;
#endif
        internal Allocator m_AllocatorLabel;

        internal C _comparator;
        public int Length
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.ValidateNonDefaultHandle(in m_Safety);
#endif
                return m_Data->m_Length;
            }
        }

        public T this[int index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return UnsafeUtility.ReadArrayElement<HeapNode>(m_Data->m_Buffer, index).value;
            }
        }

        [BurstDiscard]
        internal static void IsUnmanagedAndThrow()
        {
            if (!UnsafeUtility.IsBlittable<T>())
                throw new ArgumentException(string.Format("{0} used in NativeCustomArray<{0}> must be blittable", typeof(T)));
            if (!UnsafeUtility.IsValidNativeContainerElementType<T>())
            {
                throw new InvalidOperationException(
                    $"{typeof(T)} used in NativeArray<{typeof(T)}> must be unmanaged (contain no managed types) and cannot itself be a native container type.");
            }
        }

        public MyNativeHeap(int capacity , Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory, C comparator = default)
        {
            _comparator = comparator;
            Allocate(capacity, allocator, out this);
            if ((options & NativeArrayOptions.ClearMemory) == NativeArrayOptions.ClearMemory)
            {
                UnsafeUtility.MemClear(m_Data->m_Buffer, (long)capacity * UnsafeUtility.SizeOf<HeapNode>());
                UnsafeUtility.MemClear(m_Data->m_Lookup_Table, (long)capacity * UnsafeUtility.SizeOf<TableIndex>());
            }

            for (int i = 0; i < capacity; i++)
            {
                UnsafeUtility.WriteArrayElement<HeapNode>(m_Data->m_Buffer, i, new HeapNode
                {
                    tableIndex = i,
                });
                UnsafeUtility.WriteArrayElement<TableIndex>(m_Data->m_Lookup_Table, i, new TableIndex
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    valid = 0,
#endif
                });

            }
        }

        static void Allocate(int capacity, Allocator allocator, out MyNativeHeap<T, C> array)
        {
            long size = UnsafeUtility.SizeOf<HeapNode>() * (long)capacity;
            // Check if this is a valid allocation.
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));

            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Length must be >= 0");

            if (size > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(capacity), $"Length * sizeof(int) cannot exceed {(object)int.MaxValue} bytes");
            IsUnmanagedAndThrow();
#endif

            array = default(MyNativeHeap<T, C>);
            // Allocate memory for our buffer.
            array.m_Data = (HeapData*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<HeapData>(), UnsafeUtility.AlignOf<HeapData>(), allocator);
            array.m_Data->m_Buffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<HeapNode>(), allocator);
            array.m_Data->m_Lookup_Table = (TableIndex*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<TableIndex>() * (long)capacity, UnsafeUtility.AlignOf<TableIndex>(), allocator);
            array.m_Data->m_Capacity = capacity;
            array.m_Data->m_Length = 0;
            array.m_AllocatorLabel = allocator;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Create a dispose sentinel to track memory leaks. 
            // An atomic safety handle is also created automatically.
            DisposeSentinel.Create(out array.m_Safety, out array.m_DisposeSentinel, 1, allocator);
#endif
        }

        [WriteAccessRequired]
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Data->m_Buffer == null)
            {
                throw new ObjectDisposedException("The NativeArray is already disposed.");
            }
            if (!UnsafeUtility.IsValidAllocator(m_AllocatorLabel))
                throw new InvalidOperationException("The NativeArray can not be Disposed because it was not allocated with a valid allocator.");

            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif

            // Free the allocated memory and reset our variables.
            m_Data->m_Buffer = null;
            m_Data->m_Capacity = 0;
            m_Data->m_Length = 0;
            UnsafeUtility.Free(m_Data->m_Buffer, m_AllocatorLabel);
            UnsafeUtility.Free(m_Data->m_Lookup_Table, m_AllocatorLabel);
            UnsafeUtility.Free(m_Data, m_AllocatorLabel);

            m_AllocatorLabel = Allocator.Invalid;
        }

        /// <summary>
        ///peek element with key
        /// </summary>
        /// <param name="key"> the key return when inserted </param>
        /// <returns> return inserted element with key</returns>
        public T Peek(int key)
        {
            TableIndex tableIndex = UnsafeUtility.ReadArrayElement<TableIndex>(m_Data->m_Lookup_Table, key);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            //ValideateTableIndex(tableIndex);
#endif
            return UnsafeUtility.ReadArrayElement<HeapNode>(m_Data->m_Buffer, tableIndex.heapIndex).value;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void ValideateTableIndex(in TableIndex index)
        {
            if (index.heapIndex >= m_Data->m_Length || index.heapIndex < 0)
            {
                throw new InvalidOperationException("The Index in Invalide");
            }
        }

        public T Pop()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
           return RemoveInHeap(0);
        }

        /// <summary>
        /// delete element with key and return deleted element
        /// </summary>
        /// <param name="key"> the key return when inserted </param>
        /// <returns></returns>
        [WriteAccessRequired]
        public T Remove(int key)
        {
            TableIndex tableIndex = UnsafeUtility.ReadArrayElement<TableIndex>(m_Data->m_Lookup_Table, key);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            //ValideateTableIndex(tableIndex);
#endif
            return RemoveInHeap(tableIndex.heapIndex);
        }

        public T RemoveInHeap(int heapIndex)
        {
            if (m_Data->m_Length <= 0 || heapIndex >= m_Data->m_Length)
                throw new InvalidOperationException("Nothing to remove");

            var removeHeapNode = UnsafeUtility.ReadArrayElement<HeapNode>(m_Data->m_Buffer, heapIndex);
            var lastHeapNode = UnsafeUtility.ReadArrayElement<HeapNode>(m_Data->m_Buffer, --m_Data->m_Length);
            UnsafeUtility.WriteArrayElement<HeapNode>(m_Data->m_Buffer, m_Data->m_Length, removeHeapNode);
            if(heapIndex != 0)
            {
                int parentIndex = (heapIndex - 1) / 2;
                var parentNode = UnsafeUtility.ReadArrayElement<HeapNode>(m_Data->m_Buffer, parentIndex);
                if(_comparator.Compare(lastHeapNode.value, parentNode.value) < 0)
                {
                    InsertAndHeapifyUp(lastHeapNode, heapIndex);
                    return removeHeapNode.value;
                }
            }
            InsertAndHeapifyDown(lastHeapNode, heapIndex);
            return removeHeapNode.value;
        }

        /// <summary>
        /// return key that can use to peek or delete later
        /// </summary>
        /// <param name="key"> value to insert </param>
        /// <returns></returns>
        [WriteAccessRequired]
        public int Insert(T value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            if (m_Data->m_Length >= m_Data->m_Capacity)
            {
                throw new InvalidOperationException("Heap is full");
            }
            int lastIndex = m_Data->m_Length++;
            HeapNode newNode = UnsafeUtility.ReadArrayElement<HeapNode>(m_Data->m_Buffer, lastIndex);
            newNode.value = value;
            InsertAndHeapifyUp(newNode, lastIndex);
            return newNode.tableIndex;
        }

        void InsertAndHeapifyUp(in HeapNode node, int start_index)
        {
            while(start_index != 0)
            {
                int parentIndex = (start_index - 1) / 2;
                var parentNode = UnsafeUtility.ReadArrayElement<HeapNode>(m_Data->m_Buffer, parentIndex);
                if (_comparator.Compare(node.value, parentNode.value) >= 0)
                    break;

                UnsafeUtility.WriteArrayElement<HeapNode>(m_Data->m_Buffer, start_index, parentNode);
                (m_Data->m_Lookup_Table + parentNode.tableIndex)->heapIndex = start_index;

                start_index = parentIndex;
            }
            UnsafeUtility.WriteArrayElement<HeapNode>(m_Data->m_Buffer, start_index, node);
            (m_Data->m_Lookup_Table + node.tableIndex)->heapIndex = start_index;
        }

        void InsertAndHeapifyDown(in HeapNode node, int start_index)
        {
            while (true)
            {
                int childLeftIndex = start_index * 2 + 1;
                int childRightIndex = start_index * 2 + 2;

                var childLeftNode = UnsafeUtility.ReadArrayElement<HeapNode>(m_Data->m_Buffer, childLeftIndex);
                var childRightNode = UnsafeUtility.ReadArrayElement<HeapNode>(m_Data->m_Buffer, childRightIndex);
                if (childLeftIndex >= m_Data->m_Length)
                    break;

                if(childRightIndex >= m_Data->m_Length || _comparator.Compare(childLeftNode.value, childRightNode.value) <=0)
                {
                    if (_comparator.Compare(node.value, childLeftNode.value) < 0)
                        break;

                    UnsafeUtility.WriteArrayElement<HeapNode>(m_Data->m_Buffer, start_index, childLeftNode);
                    (m_Data->m_Lookup_Table + childLeftNode.tableIndex)->heapIndex = start_index;

                    start_index = childLeftIndex;
                } else
                {
                    if (_comparator.Compare(node.value, childRightNode.value) < 0)
                        break;

                    UnsafeUtility.WriteArrayElement<HeapNode>(m_Data->m_Buffer, start_index, childRightNode);
                    (m_Data->m_Lookup_Table + childRightNode.tableIndex)->heapIndex = start_index;

                    start_index = childRightIndex;
                }
            }
            UnsafeUtility.WriteArrayElement<HeapNode>(m_Data->m_Buffer, start_index, node);
            (m_Data->m_Lookup_Table + node.tableIndex)->heapIndex = start_index;
        }

        [BurstDiscard]
        public void PrintOut()
        {
            for(int i=0; i < m_Data->m_Length; i++)
            {
                HeapNode node = UnsafeUtility.ReadArrayElement<HeapNode>(m_Data->m_Buffer, i);
                TableIndex tableIndex = UnsafeUtility.ReadArrayElement<TableIndex>(m_Data->m_Lookup_Table, node.tableIndex);
                UnityEngine.Debug.Log($"{i}: {node.value} tableIndex {node.tableIndex} heapIndex {tableIndex.heapIndex}");
            }
        }

     
    }
}
