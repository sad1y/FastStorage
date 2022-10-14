using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FeatureStorage.Memory;

namespace FeatureStorage;

public class PairFeatureAggregator<T> : IDisposable where T : notnull
{
    private readonly int _idSize;
    private readonly int _featureSize;
    private readonly Dictionary<T, long> _index;
    private readonly ContiguousAllocator _memory;

    private const int MaxBlockSize = 256 * 1024 * 1024;

    public PairFeatureAggregator(int capacity, int idSize, int featureSize)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (featureSize <= 0) throw new ArgumentOutOfRangeException(nameof(featureSize));

        _idSize = idSize;
        _featureSize = featureSize;

        var size = capacity * (featureSize + idSize);
        _index = new Dictionary<T, long>(capacity);
        _memory = new ContiguousAllocator((int)Math.Min(size, MaxBlockSize));
    }

    public void Add(T key, ReadOnlySpan<byte> id, ReadOnlySpan<byte> data)
    {
        if (_index.TryGetValue(key, out var offset))
            Extend(offset, id, data);
        else
        {
            offset = Add(id, data);
            _index.Add(key, offset);
        }
    }

    public readonly ref struct Entry
    {
        public readonly ReadOnlySpan<byte> Id;
        public readonly ReadOnlySpan<byte> Features;

        public Entry(ReadOnlySpan<byte> id, ReadOnlySpan<byte> features)
        {
            Id = id;
            Features = features;
        }
    }

    public unsafe ref struct Iterator
    {
        private readonly int _idSize;
        private readonly int _dataSize;
        private readonly Chain _chain;
        private void* _currentNodePtr;
        private int _processed;

        internal Iterator(ref Chain chain, int idSize, int dataSize)
        {
            _chain = chain;
            _idSize = idSize;
            _dataSize = dataSize;
            _processed = -1;
            fixed (void* ptr = &chain) _currentNodePtr = ptr;
        }

        public bool MoveNext()
        {
            if (_processed == _chain.Count - 1)
                return false;

            if (_processed == -1)
            {
                _currentNodePtr = (Node*)((byte*)_currentNodePtr + sizeof(Chain));
            }
            else
            {
                ref var node = ref *(Node*)_currentNodePtr;
                _currentNodePtr = new IntPtr(node.Next).ToPointer();
            }

            _processed++;
            return true;
        }

        public Entry GetCurrent()
        {
            var ptr = (byte*)_currentNodePtr + sizeof(Node);
            var idSpan = new ReadOnlySpan<byte>(ptr, _idSize);
            var dataSpan = new ReadOnlySpan<byte>(ptr + _idSize, _dataSize);
            return new Entry(idSpan, dataSpan);
        }
    }

    public delegate void IterateCallback(T key, int elementCount, ref Iterator iterator);

    public void Iterate(IterateCallback callback)
    {
        foreach (var (key, offset) in _index)
        {
            ref var chain = ref GetChain(offset);
            var iterator = new Iterator(ref chain, _idSize, _featureSize);
            callback(key, chain.Count, ref iterator);
        }
    }

    private unsafe void AddNode(ref Chain chain, ReadOnlySpan<byte> id, ReadOnlySpan<byte> data)
    {
        var ptr = _memory.Allocate(sizeof(Node) + _idSize + _featureSize);
        var buffer = new Span<byte>((ptr + sizeof(Node)).ToPointer(), _idSize + _featureSize);
        id.CopyTo(buffer[.._idSize]);
        data.CopyTo(buffer.Slice(_idSize, _featureSize));
        var offset = ptr.ToInt64();

        // update next property for previous element in a chain if it is not empty
        if (chain.Count != 0)
        {
            ref var latest = ref GetChainNode(chain.Tail);
            latest.Next = offset;
        }

        ref var node = ref *(Node*)ptr;
        
        node.Next = 0;
        chain.Count += 1;
        chain.Tail = offset;
    }

    private unsafe long Add(ReadOnlySpan<byte> id, ReadOnlySpan<byte> data)
    {
        var ptr = _memory.Allocate(sizeof(Chain));
        ref var chain = ref *(Chain*)ptr;
        chain.Count = 0;
        AddNode(ref chain, id, data);
        
        return ptr.ToInt64();
    }

    private void Extend(long offset, ReadOnlySpan<byte> id, ReadOnlySpan<byte> data)
    {
        ref var chain = ref GetChain(offset);
        AddNode(ref chain, id, data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe ref Chain GetChain(long offset) => ref *(Chain*)(byte*)new IntPtr(offset);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe ref Node GetChainNode(long offset) => ref *(Node*)(byte*)new IntPtr(offset);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _memory.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Chain
    {
        public long Tail;
        public ushort Count;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Node
    {
        public long Next;

        public bool IsFinal => Next == 0;
    }
}