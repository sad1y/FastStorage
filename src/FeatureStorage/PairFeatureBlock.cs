using FeatureStorage.Memory;

namespace FeatureStorage;

public readonly unsafe struct PairFeatureBlock<T> where T : unmanaged
{
    private readonly MemoryAllocator _allocator;
    private readonly IntPtr _ptr;
    private readonly int _count;
    private readonly int _featureCount;

    internal PairFeatureBlock(MemoryAllocator allocator, IntPtr ptr, int count, int featureCount)
    {
        _allocator = allocator;
        _ptr = ptr;
        _count = count;
        _featureCount = featureCount;
    }

    public ReadOnlySpan<T> GetMeta()
    {
        return new ReadOnlySpan<T>(_ptr.ToPointer(), _count);
    }

    public ReadOnlySpan<float> GetFeatureMatrix()
    {
        return new ReadOnlySpan<float>((_ptr + sizeof(T) * _count).ToPointer(), _count * _featureCount);
    }

    public ReadOnlySpan<float> GetFeatureVector(int index)
    {
        if (index <= 0 || _count <= index) throw new ArgumentOutOfRangeException(nameof(index));

        var offset = _ptr + sizeof(T) * _count + index * _featureCount * sizeof(float);

        return new ReadOnlySpan<float>(offset.ToPointer(), _featureCount);
    }

    public int Count => _count;

    public int FeatureCount => _featureCount;

    public void Release()
    {
        _allocator.Free(_ptr);
    }
}