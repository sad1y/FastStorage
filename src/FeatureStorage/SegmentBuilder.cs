using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FeatureStorage;
/*

    public class SegmentBuilder<T> : IDisposable where T : unmanaged 
    {
        private readonly Dictionary<T, long> _index;
        private readonly UnmanagedMemory _memory;

        public SegmentBuilder(int capacity, uint elementSize)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (elementSize <= 0) throw new ArgumentOutOfRangeException(nameof(elementSize));

            var size = capacity * elementSize;
            _index = new Dictionary<T, long>(capacity);
            _memory = new UnmanagedMemory((int)Math.Min(size, int.MaxValue - 1));
        }

        private unsafe void AddNode(ref Chain chain, ReadOnlySpan<byte> seq)
        {
            var ptr = _memory.Allocate(sizeof(Node) + seq.Length);
            var buffer = new Span<byte>((ptr + sizeof(Node)).ToPointer(), seq.Length);
            seq.CopyTo(buffer);
            var offset = _memory.Start.GetLongOffset(ptr);

            if (chain.Count != 0)
            {
                ref var latest = ref *(Node*)((byte*)_memory.Start.ToPointer() + chain.Tail);
                latest.Next = offset;
            }

            chain.Count += 1;
            chain.Tail = offset;
        }

        public void Add(T key, ReadOnlySpan<byte> data)
        {
            if (_index.TryGetValue(key, out var offset))
                Extend(offset, data);
            else
            {
                offset = Add(data);
                _index.Add(key, offset);
            }
        }

        public Segment Build()
        {
            var memory = new UnmanagedMemory((int)Math.Min(_memory.GetMemoryUsage(), int.MaxValue - 1));

            foreach (var (key, offset) in _index)
                _index[key] = CompactEntryBlock(ref GetChain(offset), memory);

            _memory.Reset();

            return new Segment(_index, memory);
        }

        /// <summary>
        /// compact a entries added earlier to create a contiguous memory block 
        /// </summary>
        /// <param name="chain">entries linked list</param>
        /// <param name="allocator">memory allocator</param>
        /// <returns></returns>
        private unsafe long CompactEntryBlock(ref Chain chain, UnmanagedMemory allocator)
        {
            if (chain.Count == 0)
                return 0;

            var chainPtr = (byte*)Unsafe.AsPointer(ref chain);

            ref var current = ref *(Node*)(chainPtr + sizeof(Chain));

            var blockPtr = allocator.Allocate(sizeof(Segment.EntryBlock) * chain.Count).ToPointer();
            Unsafe.Write(blockPtr, chain.Count); // aka EntryBlock.Count = chain.Count 

            while (!current.IsFinal)
            {
                var ptr = (byte*)Unsafe.AsPointer(ref current);
                var entryPtr = allocator.Allocate(current.Size);
                Unsafe.Write(entryPtr.ToPointer(), chain.Count); // aka Entry.Size = current.Size

                var src = new ReadOnlySpan<byte>(ptr + sizeof(Node), current.Size);
                var dest = new Span<byte>((entryPtr + sizeof(Segment.Entry)).ToPointer(), current.Size);
                src.CopyTo(dest);
                current = ref GetChainNode(current.Next);
            }

            return allocator.Start.GetOffset(new IntPtr(blockPtr));
        }

        private unsafe uint Add(ReadOnlySpan<byte> seq)
        {
            var ptr = _memory.Allocate(sizeof(Chain));
            ref var chain = ref *(Chain*)ptr;
            AddNode(ref chain, seq);
            return (uint)_memory.Start.GetLongOffset(ptr);
        }

        private void Extend(long offset, ReadOnlySpan<byte> seq)
        {
            ref var chain = ref GetChain(offset);
            AddNode(ref chain, seq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe ref Chain GetChain(long offset) => ref *(Chain*)((byte*)_memory.Start.ToPointer() + offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void* GetChainNodePtr(long offset) => (byte*)_memory.Start.ToPointer() + offset + sizeof(Chain);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe ref Node GetChainNode(long offset) => ref *(Node*)GetChainNodePtr(offset);

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _memory.Dispose();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Chain
        {
            public long Tail;
            public ushort Count;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Node
        {
            public ushort Size;

            public long Next;

            public bool IsFinal => Next == 0;
        }
    }
*/