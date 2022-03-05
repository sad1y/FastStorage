using FeatureStorage.Memory;

namespace FeatureStorage;

public class PairFeatureVault<T> where T : unmanaged
{
    private readonly int _featureCount;

    private readonly PairFeatureCodec<T> _codec;

    private readonly Dictionary<T, IntPtr> _index = new();
    private readonly RecycleRegionAllocator _builderBuffer = new(16 * 1024 * 1024);
    private readonly ContiguousAllocator _storageMemory;

    public delegate void CreateBlock(ref PairFeatureBlockBuilder<T> builder);

    public PairFeatureVault(int featureCount, PairFeatureCodec<T> codec, int blockSize = 16 * 1024 * 1024)
    {
        _storageMemory = new ContiguousAllocator(blockSize);
        _featureCount = featureCount;
        _codec = codec;
    }

    public void AddOrUpdate(T key, int capacity, CreateBlock blockCreator)
    {
        var blockBuilder = new PairFeatureBlockBuilder<T>(_builderBuffer, _featureCount, capacity);
        blockCreator(ref blockBuilder);
        var block = blockBuilder.ToBlock();

        try
        {
            // calc how many at most bytes are needed
            var encoderBufferSize = blockBuilder.GetAllocatedSize();
            var ptr = _storageMemory.Allocate(encoderBufferSize);
            if (!_codec.TryEncode(ref block, ptr, out var written))
                throw new IOException("cannot encode block");

            _index[key] = ptr;
            // return unused memory 
            _storageMemory.Return(encoderBufferSize - written);
        }
        finally
        {
            blockBuilder.Release();
        }
    }

    public bool TryGet(T key, out PairFeatureBlock<T> featureBlock)
    {
        // ReSharper disable once InvertIf
        if (_index.TryGetValue(key, out var ptr))
        {
            _codec.TryDecode(ptr, out featureBlock, out _);
            return true;
        }

        featureBlock = new PairFeatureBlock<T>();
        return false;
    }
}