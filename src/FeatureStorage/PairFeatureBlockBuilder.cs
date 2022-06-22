using System.Runtime.CompilerServices;
using FeatureStorage.Memory;

namespace FeatureStorage;

/*
 * var storage = new CompressedFeatureStorage();
 * using(var writer = storage.CreateWriter()) {
 *  writer.Add(key, value);
 *  writer.Add(key, value);
 *  writer.Add(key, value);
 * }
 * ...
 * storage.TryGet(key, out var featureBlock);
 */

public unsafe struct PairFeatureBlockBuilder<T> where T : unmanaged
{
    private readonly MemoryAllocator _memory;
    private readonly int _featureCount;
    private readonly int _capacity;
    private readonly int _memorySize;
    private readonly IntPtr _ptr;
    private int _count;

    public PairFeatureBlockBuilder(MemoryAllocator memory, int featureCount, int capacity)
    {
        _memory = memory;
        _featureCount = featureCount;
        _capacity = capacity;
        _memorySize = capacity * (featureCount * sizeof(float) + sizeof(T));
        _ptr = _memory.Allocate(_memorySize);
        _count = 0;
    }

    public void AddFeatures(T tag, ReadOnlySpan<float> values)
    {
        if (_capacity == _count)
            throw new IOException("capacity exceeded");

        Unsafe.Write((_ptr + _count * sizeof(T)).ToPointer(), tag);

        var offset = _count * _featureCount * sizeof(float) + sizeof(T) * _capacity;

        fixed (void* srcPtr = values)
            Unsafe.CopyBlock((_ptr + offset).ToPointer(), srcPtr, (uint)(_featureCount * sizeof(float)));

        _count++;
    }

    public PairFeatureBlock<T> ToBlock()
    {
        return new PairFeatureBlock<T>(_memory, _ptr, _count, _featureCount);
    }

    public void Release()
    {
        _memory.Free(_ptr);
    }

    public int GetAllocatedSize()
    {
        return _memorySize;
    }
}