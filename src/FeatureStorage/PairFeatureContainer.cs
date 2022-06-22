using FeatureStorage.Extensions;
using FeatureStorage.Memory;

namespace FeatureStorage;

public abstract class PairFeatureContainer<TKey, TTag>
    where TKey : unmanaged
    where TTag : unmanaged
{
    private readonly ContiguousAllocator _allocator;

    protected readonly RecycleRegionAllocator TempAllocator = new(16 * 1024 * 1024);
    protected readonly Index<TKey> KeyIndex;
    protected readonly int FeatureCount;

    public delegate void CreateBlock(ref PairFeatureBlockBuilder<TTag> builder);

    protected PairFeatureContainer(int featureCount, int blockSize = 16 * 1024 * 1024)
    {
        _allocator = new ContiguousAllocator(blockSize);
        FeatureCount = featureCount;
        KeyIndex = CreateIndex();
    }

    protected abstract Index<TKey> CreateIndex();
    
    public void AddOrUpdate(TKey key, int capacity, CreateBlock blockCreator)
    {
        var blockBuilder = new PairFeatureBlockBuilder<TTag>(TempAllocator, FeatureCount, capacity);
        blockCreator(ref blockBuilder);
        var block = blockBuilder.ToBlock();

        try
        {
            // calc how many at most bytes needed
            var encoderBufferSize = blockBuilder.GetAllocatedSize();
            var ptr = _allocator.Allocate(encoderBufferSize);

            if (!TryEncode(ref block, ptr, out var written))
                throw new IOException("cannot encode block");

            KeyIndex.Update(key, _allocator.Start.GetLongOffset(ptr));
            // return unused memory 
            _allocator.Return(encoderBufferSize - written);
        }
        finally
        {
            blockBuilder.Release();
        }
    }

    public bool TryGet(TKey key, out PairFeatureBlock<TTag> featureBlock)
    {
        if (KeyIndex.TryGetValue(key, out var offset) &&
            TryDecode(_allocator.Start.MoveBy(offset), out featureBlock, out _))
        {
            return true;
        }

        featureBlock = new PairFeatureBlock<TTag>();
        return false;
    }

    protected abstract bool TryEncode(ref PairFeatureBlock<TTag> pairFeatureBlock, IntPtr dest, out int written);

    protected abstract bool TryDecode(IntPtr src, out PairFeatureBlock<TTag> pairFeatureBlock, out int read);

    public abstract void Serialize(Stream stream);

    public abstract PairFeatureContainer<TKey, TTag> Deserialize(Stream stream);
}