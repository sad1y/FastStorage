using System.Runtime.CompilerServices;
using FeatureStorage.Memory;

namespace FeatureStorage;

public readonly unsafe struct PairFeatureBlock<TId> where TId : unmanaged
{
    private readonly MemoryAllocator _allocator;
    private readonly IntPtr _ptr;
    private readonly int _count;
    private readonly int _featureCount;

    public PairFeatureBlock(MemoryAllocator allocator, int count, int featureCount)
    {
        _allocator = allocator;
        _ptr = allocator.Allocate(count * (featureCount * sizeof(float) + sizeof(TId)));
        _count = count;
        _featureCount = featureCount;
    }

    public Span<TId> GetIds()
    {
        return new Span<TId>(_ptr.ToPointer(), _count);
    }

    public Span<float> GetFeatureMatrix()
    {
        return new Span<float>((_ptr + sizeof(TId) * _count).ToPointer(), _count * _featureCount);
    }

    public ReadOnlySpan<float> GetFeatureVector(int index)
    {
        if (index < 0 || _count <= index) throw new ArgumentOutOfRangeException(nameof(index));

        var offset = _ptr + sizeof(TId) * _count + index * _featureCount * sizeof(float);

        return new ReadOnlySpan<float>(offset.ToPointer(), _featureCount);
    }

    internal void Insert(int position, TId id, ReadOnlySpan<float> features)
    {
        Unsafe.Write((_ptr + position * sizeof(TId)).ToPointer(), id);
        
        var offset = position * _featureCount * sizeof(float) + sizeof(TId) * _count;
        
        fixed (void* srcPtr = features)
            Unsafe.CopyBlock((_ptr + offset).ToPointer(), srcPtr, (uint)(_featureCount * sizeof(float)));
    }

    public int Count => _count;

    public int FeatureCount => _featureCount;

    public int GetAllocatedSize()
    {
        return (_featureCount * sizeof(float) + sizeof(TId)) * _count;
    }
    
    public void Release()
    {
        if(_ptr != IntPtr.Zero)
            _allocator.Free(_ptr);
    }
}