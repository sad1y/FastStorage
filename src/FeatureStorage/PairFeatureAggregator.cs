using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FeatureStorage.Memory;

namespace FeatureStorage;

public class PairFeatureAggregator<TKey, TId, TFeatureCodec> : IDisposable
    where TId : unmanaged
    where TFeatureCodec : IFeatureCodec
{
    private readonly int _featureCount;
    private readonly int _maxBlockSize;
    private readonly TFeatureCodec _codec;
    private readonly Dictionary<TKey, long> _index;
    private readonly PinnedAllocator _memory;

    private const int MaxBlockSize = 256 * 1024 * 1024;

    public unsafe PairFeatureAggregator(int capacity, int featureCount, int maxBlockSize, TFeatureCodec codec)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (featureCount <= 0) throw new ArgumentOutOfRangeException(nameof(featureCount));
        if (maxBlockSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxBlockSize));

        _featureCount = featureCount;
        _maxBlockSize = maxBlockSize;
        _codec = codec;

        var size = capacity * (featureCount * sizeof(float) + sizeof(TId));
        _index = new Dictionary<TKey, long>(capacity);
        _memory = new PinnedAllocator(Math.Min(size, MaxBlockSize));
    }

    public bool TryAdd(TKey key, TId id, ReadOnlySpan<float> features)
    {
        if (_index.TryGetValue(key, out var offset))
        {
            ref var chain = ref GetChain(offset);
            if (chain.Count >= _maxBlockSize)
                return false;
            
            AddNode(ref chain, id, features);
        }
        else
        {
            offset = Add(id, features);
            _index.Add(key, offset);
        }

        return true;
    }

    public PairFeatureContainer<TCodec, TIndex, TKey, TId> BuildContainer<TCodec, TIndex>
        (TCodec codec, TIndex index, int blockSize = 16 * 1024 * 1024)
        where TCodec : IPairFeatureCodec<TId>
        where TIndex : IIndex<TKey>
    {
        var container = new PairFeatureContainer<TCodec, TIndex, TKey, TId>(codec, index, _featureCount, blockSize);
        var ids = ArrayPool<TId>.Shared.Rent(_maxBlockSize);
        var featureMatrix = ArrayPool<float>.Shared.Rent(_maxBlockSize * _featureCount);

        try
        {
            foreach (var (key, offset) in _index)
            {
                ref var chain = ref GetChain(offset);
                var iterator = new Iterator(ref chain);

                var count = 0;
                while (iterator.MoveNext())
                {
                    var entry = iterator.GetCurrent();
                    ids[count] = entry.Id;
                    
                    var vec = featureMatrix.AsSpan(_featureCount * count, _featureCount);
                    if (!_codec.TryDecode(entry.Features, vec, out _))
                        throw new FormatException("Cannot decode feature matrix");

                    count++;
                }
                
                Debug.Assert(count == chain.Count);

                container.AddOrUpdate(key, chain.Count,
                    static (ref PairFeatureBlockBuilder<TId> builder,
                        (TId[] ids, float[] matrix, int size, int featureCount) state) =>
                    {
                        for (var i = 0; state.size > i; i++)
                        {
                            builder.AddFeatures(
                                state.ids[i],
                                state.matrix.AsSpan(i * state.featureCount, state.featureCount)
                            );
                        }
                    },
                    (ids, featureMatrix, chain.Count, _featureCount));
            }
        }
        finally
        {
            ArrayPool<TId>.Shared.Return(ids);
            ArrayPool<float>.Shared.Return(featureMatrix);
        }

        return container;
    }

    private readonly ref struct Entry
    {
        public readonly TId Id;
        public readonly ReadOnlySpan<byte> Features;

        public Entry(TId id, ReadOnlySpan<byte> features)
        {
            Id = id;
            Features = features;
        }
    }

    private unsafe ref struct Iterator
    {
        private readonly Chain _chain;
        private void* _currentNodePtr;
        private int _processed;

        internal Iterator(ref Chain chain)
        {
            _chain = chain;
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
            var offset = 0;
            var id = Unsafe.Read<TId>(ptr + offset);
            offset += sizeof(TId);
            var size = Unsafe.Read<int>(ptr + offset);
            offset += sizeof(int);
            var dataSpan = new ReadOnlySpan<byte>(ptr + offset, size);
            return new Entry(id, dataSpan);
        }
    }

    private unsafe void AddNode(ref Chain chain, TId id, ReadOnlySpan<float> features)
    {
        var bufferSize = sizeof(float) * features.Length;
        Span<byte> buffer = stackalloc byte[bufferSize];

        if (!_codec.TryEncode(features, buffer, out var written))
            throw new FormatException("Cannot encode features");

        var data = buffer[..written];
        
        var entrySize = sizeof(TId) + data.Length + sizeof(int);
        var ptr = _memory.Allocate(sizeof(Node) + entrySize);
        {
            var offset = sizeof(Node);
            Unsafe.Write((ptr + offset).ToPointer(), id);
            offset += sizeof(TId);
            Unsafe.Write((ptr + offset).ToPointer(), data.Length);
            offset += sizeof(int);
            data.CopyTo(new Span<byte>((ptr + offset).ToPointer(), data.Length));
        }
        {
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
    }

    private unsafe long Add(TId id, ReadOnlySpan<float> data)
    {
        var ptr = _memory.Allocate(sizeof(Chain));
        ref var chain = ref *(Chain*)ptr;
        chain.Count = 0;
        AddNode(ref chain, id, data);

        return ptr.ToInt64();
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
    private struct Chain
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