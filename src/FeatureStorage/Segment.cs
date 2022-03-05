using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FeatureStorage.Memory;

namespace FeatureStorage;

public class Segment<T> where T : unmanaged
{
    private readonly Dictionary<T, long> _index;
    private readonly ContiguousAllocator _memory;

    internal Segment(Dictionary<T, long> index, ContiguousAllocator memory)
    {
        _index = index;
        _memory = memory;
    }

    public unsafe bool TryGetValue(T key, out Iterator iterator)
    {
        if (!_index.TryGetValue(key, out var offset))
        {
            iterator = Iterator.Empty;
            return false;
        }

        var ptr = (byte*)_memory.Start.ToPointer() + offset;

        ref var entryBlock = ref *(EntryBlock*)ptr;

        iterator = new Iterator(_memory.Start, entryBlock.Count, offset);

        return true;
    }

    public struct Iterator
    {
        private readonly IntPtr _base;
        private readonly uint _count;
        private long _offset;
        private uint _current;

        internal Iterator(IntPtr basePtr, uint count, long offset)
        {
            _base = basePtr;
            _offset = offset;
            _count = count;
            _current = 0;
        }

        public static readonly Iterator Empty = new Iterator(IntPtr.Zero, 0, 0);

        public bool IsEmpty => _count == 0;

        public bool MoveNext()
        {
            if (_count <= _offset) return false;

            ref var entry = ref ReadEntry();
            _offset += entry.Size;
            _current++;
            return true;
        }

        private unsafe ref Entry ReadEntry() => ref *(Entry*)((byte*)_base.ToPointer() + _offset);

        public unsafe ReadOnlySpan<byte> Current()
        {
            ref var entry = ref ReadEntry();
            return new ReadOnlySpan<byte>((byte*)Unsafe.AsPointer(ref entry) + sizeof(Entry), (int)entry.Size);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct Entry
    {
        public readonly uint Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct EntryBlock
    {
        public readonly ushort Count;
    }
}