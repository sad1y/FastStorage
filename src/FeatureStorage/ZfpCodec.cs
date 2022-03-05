using System.Runtime.CompilerServices;
using FeatureStorage.Memory;
using ZfpDotnet;

namespace FeatureStorage;

public class ZfpCodec<T> : PairFeatureCodec<T> where T : unmanaged
{
    private readonly MemoryAllocator _allocator;
    private readonly double _tolerance;

    public ZfpCodec(MemoryAllocator allocator, double tolerance)
    {
        _allocator = allocator;
        _tolerance = tolerance;
    }

    public override unsafe bool TryEncode(ref PairFeatureBlock<T> pairFeatureBlock, IntPtr dest, out int written)
    {
        written = 0;
        Unsafe.Write(dest.ToPointer(), pairFeatureBlock.Count);
        written += sizeof(int);
        Unsafe.Write((dest + written).ToPointer(), pairFeatureBlock.FeatureCount);
        written += sizeof(int);

        // write key as is, do not compress it
        var metaSizeInBytes = sizeof(T) * pairFeatureBlock.Count;
        fixed (void* metaPtr = pairFeatureBlock.GetMeta())
            Buffer.MemoryCopy(metaPtr, (dest + written).ToPointer(), metaSizeInBytes, metaSizeInBytes);

        written += metaSizeInBytes;

        var featureMatrix = pairFeatureBlock.GetFeatureMatrix();
        var span = new Span<byte>((dest + written).ToPointer(), featureMatrix.Length * sizeof(float));
        var compressionSize = ZfpNative.Compress(featureMatrix, span, _tolerance);

        written += (int)compressionSize;
        return true;
    }

    public override unsafe bool TryDecode(IntPtr src, out PairFeatureBlock<T> pairFeatureBlock, out int read)
    {
        read = 0;
        var count = Unsafe.Read<int>(src.ToPointer());
        read += sizeof(int);
        var featureCount = Unsafe.Read<int>((src + read).ToPointer());
        read += sizeof(int);

        var blockSize = count * (featureCount * sizeof(float) + sizeof(T));
        var dest = _allocator.Allocate(blockSize);
        pairFeatureBlock = new PairFeatureBlock<T>(_allocator, dest, count, featureCount);

        var metaSize = count * sizeof(T);

        fixed (void* ptr = pairFeatureBlock.GetMeta())
        {
            Buffer.MemoryCopy((src + read).ToPointer(), ptr, metaSize, metaSize);
        }

        read += metaSize;

        var encodedFeatureSetStream = new ReadOnlySpan<byte>((src + read).ToPointer(), blockSize - read);
        
        fixed (void* ptr = pairFeatureBlock.GetFeatureMatrix())
        {
            ZfpNative.Decompress(encodedFeatureSetStream,
                new Span<byte>(ptr, count * featureCount * sizeof(float)), out _, out var readFromCompression);
            read += (int)(readFromCompression * sizeof(float));
        }

        return true;
    }
}