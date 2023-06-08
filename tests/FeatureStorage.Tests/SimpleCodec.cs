using System;
using FeatureStorage.Memory;

namespace FeatureStorage.Tests;

public class SimpleCodec : IPairFeatureCodec<long>
{
    public unsafe bool TryEncode(ref PairFeatureBlock<long> pairFeatureBlock, Span<byte> dest, out int written)
    {
        var offset = 0;
        fixed (void* destPtr = dest)
        fixed (void* keysPtr = pairFeatureBlock.GetIds())
        fixed (void* featurePtr = pairFeatureBlock.GetFeatureMatrix())
        {
            var idSize = pairFeatureBlock.Count * sizeof(long);
            Buffer.MemoryCopy(keysPtr, (new IntPtr(destPtr) + offset).ToPointer(), dest.Length, idSize);
            offset += idSize;

            var featureSize = pairFeatureBlock.GetFeatureMatrix().Length * sizeof(float);
            Buffer.MemoryCopy(featurePtr, (new IntPtr(destPtr) + offset).ToPointer(), dest.Length, featureSize);
            offset += featureSize;
        }

        written = offset;
        return true;
    }

    private static readonly RecycleRegionAllocator Allocator = new(4096);

    public bool TryDecode(ReadOnlySpan<byte> src, ref PairFeatureBlock<long> pairFeatureBlock, out int read)
    {
        const int offset = sizeof(int);
        var size = pairFeatureBlock.Count;
        var structSize = size * sizeof(long) + sizeof(float) * pairFeatureBlock.FeatureCount * size;
        unsafe
        {
            fixed (void* ptr = src[offset..])
            {
                var ids = new Span<long>(ptr, size);
                pairFeatureBlock = new PairFeatureBlock<long>(Allocator, size, pairFeatureBlock.FeatureCount);
                ids.CopyTo(pairFeatureBlock.GetIds());

                var features = new Span<float>((new IntPtr(ptr) + sizeof(long) * size).ToPointer(),
                    size * pairFeatureBlock.FeatureCount);
                features.CopyTo(pairFeatureBlock.GetFeatureMatrix());
            }
        }

        read = offset + structSize;

        return true;
    }

    public int Stamp => 123;

    public byte Version => 1;
    public int MetaSize => 32;
}