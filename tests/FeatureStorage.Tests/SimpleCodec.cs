using System;
using FeatureStorage.Memory;

namespace FeatureStorage.Tests;

public class SimpleCodec : IPairFeatureCodec<long>
{
    public unsafe bool TryEncode(ref PairFeatureBlock<long> pairFeatureBlock, Span<byte> dest, out int written)
    {
        var offset = 0;
        fixed (void* destPtr = dest)
        {
            var ids = new Span<long>(destPtr, pairFeatureBlock.Count);
            pairFeatureBlock.GetIds().CopyTo(ids);
            offset += ids.Length * sizeof(long);

            var features = new Span<float>((byte*)destPtr + offset, pairFeatureBlock.GetFeatureMatrix().Length);
            pairFeatureBlock.GetFeatureMatrix().CopyTo(features);
            offset += features.Length * sizeof(float);
        }

        written = offset;
        return true;
    }

    private static readonly RecycleRegionAllocator Allocator = new(4096);

    public bool TryDecode(ReadOnlySpan<byte> src, ref PairFeatureBlock<long> pairFeatureBlock, out int read)
    {
        var size = pairFeatureBlock.Count;
        var structSize = size * sizeof(long) + sizeof(float) * pairFeatureBlock.FeatureCount * size;
        unsafe
        {
            fixed (void* ptr = src)
            {
                var ids = new Span<long>(ptr, size);
                pairFeatureBlock = new PairFeatureBlock<long>(Allocator, size, pairFeatureBlock.FeatureCount);
                ids.CopyTo(pairFeatureBlock.GetIds());

                var features = new Span<float>((new IntPtr(ptr) + sizeof(long) * size).ToPointer(),
                    size * pairFeatureBlock.FeatureCount);
                features.CopyTo(pairFeatureBlock.GetFeatureMatrix());
            }
        }

        read = structSize;

        return true;
    }

    public int Stamp => 123;

    public byte Version => 1;
    public int MetaSize => 32;
}