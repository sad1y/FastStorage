using System.Diagnostics;
using System.Runtime.CompilerServices;
using FeatureStorage.Memory;

namespace FeatureStorage;

/*
 * var storage = new CompressedFeatureStorage();
 * using(var builder = storage.CreateWriter()) {
 *  writer.AddFeatures(key, value);
 *  writer.AddFeatures(key, value);
 *  writer.AddFeatures(key, value);
 * }
 * ...
 * storage.TryGet(key, out var featureBlock);
 */

public struct PairFeatureBlockBuilder<T> where T : unmanaged
{
    private readonly PairFeatureBlock<T> _block;
    private int _count;

    public PairFeatureBlockBuilder(MemoryAllocator memory, int featureCount, int capacity)
    {
        _count = 0;
        _block = new PairFeatureBlock<T>(memory, capacity, featureCount);
    }

    public void AddFeatures(T id, ReadOnlySpan<float> values)
    {
        Debug.Assert(values.Length == _block.FeatureCount);
        if (_block.Count == _count)
            throw new IOException("capacity exceeded");

        _block.Insert(_count++, id, values);
    }

    public PairFeatureBlock<T> ToBlock()
    {
        return _block;
    }
}