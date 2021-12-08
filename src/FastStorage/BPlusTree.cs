using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FastStorage
{
    public class BPlusTree : IDisposable
    {
        private readonly byte _nodeCapacity;
        private readonly int _nodePerMemBlock;

        private readonly MemBlock[] _blocks = new MemBlock [128];

        private int _currentBlock = -1;
        private IntPtr _rootPtr;

        private const int EmptyNode = 0;

        public BPlusTree(byte nodeCapacity = 16, int nodePerMemBlock = 1024)
        {
            if (nodeCapacity < 3) throw new ArgumentOutOfRangeException(nameof(nodeCapacity));
            _nodeCapacity = nodeCapacity;
            _nodePerMemBlock = nodePerMemBlock;

            AllocateNewMemBlock();
            // create root
            UpdateRoot(ref CreateNode(NodeFlag.Leaf));
        }

        private void UpdateRoot(ref Node root)
        {
            _rootPtr = root._ptr;
        }

        private void AllocateNewMemBlock()
        {
            var nodeSize = GetNodeSize();
            var blockSize = nodeSize * _nodePerMemBlock;
            var ptr = Marshal.AllocHGlobal(blockSize);
            _blocks[++_currentBlock] = new MemBlock(ptr, blockSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe int GetNodeSize() => sizeof(Node) + sizeof(Leaf) * _nodeCapacity;

        private IntPtr AllocateNode()
        {
            var size = GetNodeSize();
            while (true)
            {
                if (_blocks[_currentBlock].FreeSpace < size)
                {
                    AllocateNewMemBlock();
                    continue;
                }

                return _blocks[_currentBlock].Reserve(size);
            }
        }

        private unsafe ref Node CreateNode(NodeFlag flag)
        {
            var ptr = AllocateNode();

            ref var node = ref *(Node*)ptr;

            node._flag = flag;
            node._left = EmptyNode;
            node._size = 0;
            node._ptr = ptr;
            node._sibling = 0;

            return ref node;
        }

        private unsafe ref Node GetRoot() => ref *(Node*)_rootPtr;

        public void PrintAsDot(Stream stream)
        {
            ref var root = ref GetRoot();

            using var writer = new DotNotationWriter(stream);

            writer.Write(ref root);
        }

        public SearchResult Search(ulong key)
        {
            ref var root = ref GetRoot();
            return root.Search(key);
        }

        public void Insert(ulong key, ulong value)
        {
            ref var root = ref GetRoot();

            var size = root._size;

            var result = InsertInto(ref root, key, value);

            if (!result.IsNodeAllocated) return;

            if (size < _nodeCapacity)
            {
                ref var newNode = ref result.GetNode();
                root.InsertRef(key, ref newNode);
            }
            else
            {
                // create new root
                ref var newRoot = ref CreateNode(NodeFlag.Container);

                // relocate root pointer
                UpdateRoot(ref newRoot);

                newRoot.SetLeftNode(ref root);
                newRoot.InsertRef(result.Key, ref result.GetNode());
            }
        }

        private InsertResult InsertInto(ref Node node, ulong key, ulong value)
        {
            if (node.IsLeaf)
            {
                if (node._size < _nodeCapacity)
                {
                    node.InsertLeaf(key, value);
                }
                else
                {
                    // if no space left
                    // create new node and redistribute 
                    ref var newNode = ref CreateNode(NodeFlag.Leaf);

                    return node.InsertAndRedistribute(key, value, ref newNode);
                }
            }
            else
            {
                // find node to insert
                var result = SeekNode(ref node, key);

                Debug.Assert(result.IsFound, "there is always must be a node to insert");

                ref var innerNode = ref result.GetNode();
                var insertResult = InsertInto(ref innerNode, key, value);

                // handle redistribution
                if (!insertResult.IsNodeAllocated) return InsertResult.NoAllocation;

                ref var newNode = ref insertResult.GetNode();

                if (node._size < _nodeCapacity)
                {
                    // simple insert
                    node.InsertRef(insertResult.Key, ref newNode);
                }
                // if no space left
                else
                {
                    // create new node and redistribute
                    ref var rightNode = ref CreateNode(NodeFlag.Container);

                    return node.RedistributeAndInsertRef(insertResult.Key, ref insertResult.GetNode(), ref rightNode);
                }
            }

            return InsertResult.NoAllocation;
        }

        private static NodeSeekResult SeekNode(ref Node node, ulong key)
        {
            if (node._size == 0)
                return NodeSeekResult.NotFound;
            
            var position = node._size >> 1;
            var start = 0;
            var end = (int)node._size;

            while (start != end)
            {
                ref var child = ref node.GetNodeRef(position);
                if (child.Key == key)
                    return new NodeSeekResult(ref node.RightNode(ref child));

                if (child.Key < key)
                {
                    if (position + 1 == end) // last node in child list
                        return new NodeSeekResult(ref node.RightNode(ref child));

                    ref var next = ref node.GetNodeRef(position + 1);
                    if (next.Key > key)
                        return new NodeSeekResult(ref node.RightNode(ref child));

                    start = position;
                    position += Math.Max(1, (end - start) >> 1);
                }
                else
                {
                    // if its first child greater that our key take left node
                    if (position == 0)
                        return new NodeSeekResult(ref node.LeftNode());

                    ref var prev = ref node.GetNodeRef(position - 1);

                    if (prev.Key <= key)
                        return new NodeSeekResult(ref node.RightNode(ref prev));

                    end = position;
                    position -= Math.Max(1, (end - start) >> 1);
                }
            }
            
            return NodeSeekResult.NotFound;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal unsafe struct Node
        {
            private static readonly int[] ElementSize = { sizeof(NodeRef), sizeof(Leaf) };

            [FieldOffset(0)] internal NodeFlag _flag;
            [FieldOffset(1)] internal byte _size;
            [FieldOffset(2)] internal readonly ushort _reserved;
            [FieldOffset(4)] internal int _sibling;
            [FieldOffset(4)] internal int _left;
            [FieldOffset(8)] internal IntPtr _ptr;

            public static readonly Node None = new() { _ptr = IntPtr.Zero };

            public bool IsLeaf => (_flag & NodeFlag.Leaf) == NodeFlag.Leaf;

            public ref Leaf GetLeaf(int pos)
            {
                var offset = sizeof(Node) + sizeof(Leaf) * pos;
                var ptr = _ptr + offset;
                return ref *(Leaf*)ptr.ToPointer();
            }

            private ulong GetKey(int pos)
            {
                var offset = sizeof(Node) + ElementSize[(int)_flag] * pos;
                var ptr = _ptr + offset;

                return Unsafe.Read<ulong>(ptr.ToPointer());
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref NodeRef GetNodeRef(int pos)
            {
                var offset = sizeof(Node) + sizeof(NodeRef) * pos;
                var ptr = _ptr + offset;
                return ref *(NodeRef*)ptr.ToPointer();
            }

            public void InsertLeaf(ulong key, ulong value)
            {
                var pos = FindInsertPosition(key);

                if (pos == -1) return;

                if (pos != _size)
                {
                    ref var leaf = ref GetLeaf(pos);
                    fixed (Leaf* ptr = &leaf)
                    {
                        var size = (_size - pos) * sizeof(Leaf);
                        Buffer.MemoryCopy(ptr, ptr + 1, size, size);
                    }
                }

                {
                    ref var leaf = ref GetLeaf(pos);
                    leaf.Key = key;
                    leaf.Value = value;
                }

                _size++;
            }

            public void InsertRef(ulong key, ref Node newNode)
            {
                var pos = FindInsertPosition(key);

                if (pos == -1) return;

                if (pos < _size)
                {
                    var ptr = (NodeRef*)(_ptr + sizeof(Node));

                    var size = (_size - pos) * sizeof(NodeRef);
                    var dest = ptr + pos + 1;
                    Buffer.MemoryCopy(ptr + pos, dest, size, size);
                }

                SetRightRef(pos, key, ref newNode);
                _size++;
            }

            public InsertResult RedistributeAndInsertRef(ulong key, ref Node value, ref Node rightNode)
            {
                Debug.Assert(!IsLeaf && !rightNode.IsLeaf);
                var middle = (_size + 1) >> 1;
                var pos = FindInsertPosition(key);

                Debug.Assert(pos != -1);

                ref var middleRef = ref GetNodeRef(middle);

                // TODO: check that case
                if (pos == middle)
                {
                    ref var middleNode = ref RightNode(ref middleRef);

                    rightNode.SetLeftNode(ref value);

                    CopyNodeRefs(middle, ref rightNode, 0, (byte)(_size - middle));
                    rightNode._size = (byte)(_size - middle);
                    _size = (byte)middle;

                    Debug.Assert(_size == middle);

                    return new InsertResult(key, ref middleNode);
                }

                // insert key 9 into
                // +-----+-----+-----+-----+-----+
                // |  1  |  3  |  5  |  8  |  10 |
                // +-----+-----+-----+-----+-----+
                // pos = 4
                // TODO: check that case (corners)
                if (pos > middle)
                {
                    ref var middleNode = ref RightNode(ref middleRef);

                    CopyNodeRefs(pos, ref rightNode, pos - middle, (byte)(_size - pos));
                    rightNode.SetRightRef(pos - (middle + 1), key, ref value);
                    CopyNodeRefs(middle + 1, ref rightNode, 0, (byte)(pos - (middle + 1)));

                    rightNode.SetLeftNode(ref middleNode);
                    rightNode._size = (byte)(_size - middle);
                    _size = (byte)middle;

                    Debug.Assert(_size == middle);

                    return new InsertResult(middleRef.Key, ref rightNode);
                }

                // insert key 2 into
                // +-----+-----+-----+-----+-----+
                // |  1  |  3  |  5  |  8  |  10 |
                // +-----+-----+-----+-----+-----+
                // pos = 1
                // TODO: check that case (corners)
                {
                    middleRef = ref GetNodeRef(middle - 1);
                    ref var middleNode = ref RightNode(ref middleRef);

                    var srcPtr = (_ptr + sizeof(Node) + pos * sizeof(NodeRef)).ToPointer();
                    var destPtr = (_ptr + sizeof(Node) + (pos + 1) * sizeof(NodeRef)).ToPointer();
                    var sizeToCopy = (middle - pos) * sizeof(NodeRef);

                    Buffer.MemoryCopy(srcPtr, destPtr, sizeToCopy, sizeToCopy);
                    SetRightRef(pos, key, ref value);

                    rightNode.SetLeftNode(ref middleNode);
                    rightNode._size = (byte)(_size - middle);
                    _size = (byte)middle;

                    Debug.Assert(_size == middle);

                    return new InsertResult(middleRef.Key, ref middleNode);
                }
            }

            public InsertResult InsertAndRedistribute(ulong key, ulong value, ref Node rightNode)
            {
                Debug.Assert(IsLeaf && rightNode.IsLeaf);

                var pos = FindInsertPosition(key);

                var middle = _size + 1 + (~_size & 1) >> 1; // add one if even 
                var leafSize = sizeof(Leaf);
                var srcPtr = (Leaf*)(_ptr + sizeof(Node));
                var destPtr = (Leaf*)(rightNode._ptr + sizeof(Node));

                // TODO: check that case (corners)
                if (pos < middle)
                {
                    var offset = middle - 1;
                    // copy from middle to right node
                    Buffer.MemoryCopy(
                        srcPtr + offset,
                        destPtr,
                        (_size - offset) * leafSize,
                        (_size - offset) * leafSize
                    );

                    // shift by one in left node
                    Buffer.MemoryCopy(
                        srcPtr + pos,
                        srcPtr + (pos + 1),
                        (middle - pos) * leafSize,
                        (middle - pos) * leafSize
                    );

                    ref var leaf = ref GetLeaf(pos);
                    leaf.Key = key;
                    leaf.Value = value;
                }
                // TODO: check that case (corners)
                else
                {
                    // insert key 7 into
                    // +-----+-----+-----+-----+-----+-----+
                    // |  1  |  3  |  5  |  8  |  10 |  12 |
                    // +-----+-----+-----+-----+-----+-----+
                    // pos = 3

                    // copy before insert position if need it
                    if (pos != _size)
                    {
                        Buffer.MemoryCopy(
                            srcPtr + pos,
                            destPtr + (_size - pos),
                            (_size - pos) * leafSize,
                            (_size - pos) * leafSize);
                    }

                    ref var leaf = ref rightNode.GetLeaf(pos - middle);
                    leaf.Key = key;
                    leaf.Value = value;

                    Buffer.MemoryCopy(srcPtr + middle,
                        destPtr,
                        (pos - middle) * leafSize,
                        (pos - middle) * leafSize);
                }

                rightNode._size = (byte)(_size + 1 - middle);
                _size = (byte)middle;

                Debug.Assert(_size == middle);

                var siblingOffset = rightNode._ptr.ToInt64() - _ptr.ToInt64();
                Debug.Assert(siblingOffset is > int.MinValue and < int.MaxValue);

                if (_sibling > 0)
                {
                    var sibling = (Node*)(_ptr + _sibling);
                    var nextSiblingOffset = rightNode._ptr.ToInt64() - sibling->_ptr.ToInt64();
                    Debug.Assert(nextSiblingOffset is > int.MinValue and < int.MaxValue);
                    sibling->_sibling = (int)nextSiblingOffset;
                }

                _sibling = (int)siblingOffset;

                return new InsertResult(rightNode.GetKey(0), ref rightNode);
            }


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetLeftNode(ref Node node)
            {
                var offset = node._ptr.ToInt64() - _ptr.ToInt64();
                Debug.Assert(offset is > int.MinValue and < int.MaxValue);
                _left = (int)offset;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetRightRef(int pos, ulong key, ref Node node)
            {
                ref var nodeRef = ref GetNodeRef(pos);
                nodeRef.Key = key;
                var offset = node._ptr.ToInt64() - _ptr.ToInt64();
                Debug.Assert(offset is > int.MinValue and < int.MaxValue);
                nodeRef.Right = (int)offset;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref Node RightNode(ref NodeRef nodeRef)
            {
                var ptr = _ptr + nodeRef.Right;
                return ref *(Node*)ptr.ToPointer();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref Node RightNode(int pos)
            {
                ref var nodeRef = ref GetNodeRef(pos);
                return ref RightNode(ref nodeRef);
            }

            public void CopyNodeRefs(int srcOffset, ref Node dest, int destOffset, byte count)
            {
                if (count <= 0) return;

                var srcSpan = new ReadOnlySpan<NodeRef>((_ptr + sizeof(Node) + srcOffset * sizeof(NodeRef)).ToPointer(),
                    count);
                var destSpan = new Span<NodeRef>((dest._ptr + sizeof(Node) + destOffset * sizeof(NodeRef)).ToPointer(),
                    count);

                for (int i = count - 1; i >= 0; i--)
                {
                    destSpan[i].Key = srcSpan[i].Key;
                    ref var nodeRef = ref GetNodeRef(srcOffset + i);
                    ref var node = ref RightNode(ref nodeRef);
                    destSpan[i].Right = (int)(node._ptr.ToInt64() - dest._ptr.ToInt64());
                }

                dest._size += count;
            }

            public ref Node LeftNode()
            {
                var ptr = _ptr + _left;
                return ref *(Node*)ptr.ToPointer();
            }


            /// <summary>
            /// find an appropriate position to insert into a sorted array
            /// </summary>
            /// <param name="node">node is which to search</param>
            /// <param name="key">value to insert</param>
            /// <returns>
            /// return n which defined as (leaves[n-1] < key and leaves[i] > key)
            /// or -1 if duplicate has found
            /// </returns>
            private int FindInsertPosition(ulong key)
            {
                if (_size == 0)
                    return 0;

                // fast path for sorted source
                var leafKey = GetKey(_size - 1);
                if (leafKey == key)
                    return -1;

                if (leafKey < key)
                    return _size;

                // bin search
                var position = _size >> 1;
                var start = 0;
                var end = (int)_size;

                while (true)
                {
                    leafKey = GetKey(position);
                    if (leafKey == key)
                        return -1;

                    if (leafKey < key)
                    {
                        if (position >= _size - 1)
                            return _size;

                        var nextChildKey = GetKey(position + 1);

                        if (key < nextChildKey)
                            return position + 1;

                        start = position;
                        position += Math.Max(1, (end - start) >> 1);
                    }
                    else
                    {
                        if (position == 0)
                            return position;

                        var prevNodeKey = GetKey(position - 1);

                        if (key > prevNodeKey)
                            return position;

                        if (position == 1)
                            return 0;

                        end = position;
                        position -= Math.Max(1, (end - start) >> 1);
                    }
                }
            }

            public override string ToString()
            {
                var sb = new StringBuilder();
                if (IsLeaf)
                {
                    var span = new ReadOnlySpan<Leaf>((_ptr + sizeof(Node)).ToPointer(), _size);
                    for (var i = 0; i < span.Length; i++)
                    {
                        sb.Append('{');
                        sb.Append(span[i].Key);
                        sb.Append('}');
                        sb.Append(',');
                    }
                }
                else
                {
                    var span = new ReadOnlySpan<NodeRef>((_ptr + sizeof(Node)).ToPointer(), _size);
                    for (var i = 0; i < span.Length; i++)
                    {
                        sb.Append('{');
                        sb.Append(span[i].Key);
                        sb.Append(':');
                        sb.Append(span[i].Right);
                        sb.Append('}');
                        sb.Append(',');
                    }
                }

                return sb.ToString();
            }

            public SearchResult Search(ulong key)
            {
                var nodeSeekResult = SeekNode(ref this, key);
                Debug.Assert(nodeSeekResult.IsFound);

                ref var node = ref nodeSeekResult.GetNode();

                while (!node.IsLeaf)
                {
                    nodeSeekResult = SeekNode(ref node, key);
                    Debug.Assert(nodeSeekResult.IsFound);
                    node = ref nodeSeekResult.GetNode();
                }

                var start = 0;
                var end = (int)node._size;
                var offset = end >> 1;
                var ptr = (Leaf*)(node._ptr + sizeof(Node)).ToPointer();

                while (start != end)
                {
                    var leaf = ptr + offset;

                    if (leaf->Key > key)
                    {
                        end = offset;
                        offset -= Math.Max(1, (end - start) >> 1);
                    }
                    else
                    {
                        if (leaf->Key == key)
                            return new SearchResult(leaf->Value, true);

                        start = offset;
                        offset += Math.Max(1, (end - start) >> 1);
                    }
                }

                return new SearchResult(0, false);
            }
        }

        public readonly struct SearchResult
        {
            public readonly bool Found;

            public readonly ulong Value;

            public SearchResult(ulong value, bool found)
            {
                Value = value;
                Found = found;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Leaf
        {
            public ulong Key;
            public ulong Value;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NodeRef
        {
            public ulong Key;
            public int Right;
        }

        [Flags]
        internal enum NodeFlag : byte
        {
            Container = 0,
            Leaf = 1,
        }

        internal readonly struct InsertResult
        {
            public readonly IntPtr Ptr;
            public bool IsNodeAllocated => Ptr != IntPtr.Zero;
            public readonly ulong Key;

            public static InsertResult NoAllocation = new(0, IntPtr.Zero);

            public InsertResult(ulong key, IntPtr ptr)
            {
                Key = key;
                Ptr = ptr;
            }

            public InsertResult(ulong key, ref Node node)
            {
                Key = key;
                Ptr = node._ptr;
            }

            public unsafe ref Node GetNode()
            {
                return ref *(Node*)Ptr.ToPointer();
            }
        }

        internal readonly struct NodeSeekResult
        {
            private readonly IntPtr _ptr;
            public bool IsFound => _ptr != IntPtr.Zero;

            public unsafe ref Node GetNode()
            {
                return ref *(Node*)_ptr.ToPointer();
            }

            public NodeSeekResult(IntPtr ptr)
            {
                _ptr = ptr;
            }

            public NodeSeekResult(ref Node node)
            {
                _ptr = node._ptr;
            }

            public static NodeSeekResult NotFound = new(IntPtr.Zero);
        }

        private struct MemBlock
        {
            private int Offset;
            private readonly int Capacity;
            public readonly IntPtr Ptr;
            public int FreeSpace => Capacity - Offset;

            public bool IsInitialized() => Ptr != IntPtr.Zero && Capacity > 0;

            public MemBlock(IntPtr ptr, int capacity)
            {
                Offset = 0;
                Capacity = capacity;
                Ptr = ptr;
            }

            public IntPtr Reserve(int size)
            {
                var ptr = Ptr + Offset;
                Offset += size;
                return ptr;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            for (var i = 0; i < _blocks.Length; i++)
            {
                if (_blocks[i].IsInitialized())
                {
                    Marshal.FreeHGlobal(_blocks[i].Ptr);
                }
            }
        }
    }
}