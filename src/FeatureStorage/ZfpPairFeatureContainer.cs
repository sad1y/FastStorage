using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace FeatureStorage;
/*
public class ZfpPairFeatureContainer<TKey, TId> : PairFeatureContainer<TKey, TId>
    where TKey : unmanaged
    where TId : unmanaged
{
    private readonly double _tolerance;

    public ZfpPairFeatureContainer(int featureCount, double tolerance, int blockSize = 16 * 1024 * 1024)
        : base(featureCount, blockSize)
    {
        _tolerance = tolerance;
    }

    protected override Index<TKey> CreateIndex()
    {
        throw new NotImplementedException();
    }

    protected override unsafe bool TryEncode(ref PairFeatureBlock<TId> pairFeatureBlock, IntPtr dest, out int written)
    {
        written = sizeof(int); // save this space. latter we will put block length here 
        Unsafe.Write((dest + written).ToPointer(), pairFeatureBlock.Count);
        written += sizeof(int);

        // write tag as is, do not compress it
        var tagSizeInBytes = sizeof(TId) * pairFeatureBlock.Count;
        fixed (void* tagPtr = pairFeatureBlock.GeTId())
            Buffer.MemoryCopy(tagPtr, (dest + written).ToPointer(), tagSizeInBytes, tagSizeInBytes);

        written += tagSizeInBytes;

        var featureMatrix = pairFeatureBlock.GetFeatureMatrix();
        var span = new Span<byte>((dest + written).ToPointer(), featureMatrix.Length * sizeof(float));
        var compressionSize = 0; // ZfpNative.Compress(featureMatrix, span, _tolerance);

        written += (int)compressionSize;

        Unsafe.Write(dest.ToPointer(), written);
        return true;
    }

    protected override unsafe bool TryDecode(IntPtr src, out PairFeatureBlock<TId> pairFeatureBlock, out int read)
    {
        read = sizeof(int); // skip read total size because we don't need it 
        var count = Unsafe.Read<int>((src + read).ToPointer());
        read += sizeof(int);

        var blockSize = count * (FeatureCount * sizeof(float) + sizeof(TId));
        var dest = TempAllocator.Allocate(blockSize);
        pairFeatureBlock = new PairFeatureBlock<TId>(TempAllocator, dest, count, FeatureCount);

        var tagSize = count * sizeof(TId);

        fixed (void* ptr = pairFeatureBlock.GeTId())
        {
            Buffer.MemoryCopy((src + read).ToPointer(), ptr, tagSize, tagSize);
        }

        read += tagSize;

        var encodedFeatureSetStream = new ReadOnlySpan<byte>((src + read).ToPointer(), blockSize - read);

        fixed (void* ptr = pairFeatureBlock.GetFeatureMatrix())
        {
            // ZfpNative.Decompress(encodedFeatureSetStream,
            //     new Span<byte>(ptr, count * FeatureCount * sizeof(float)), out _, out var readFromCompression);
            // read += (int)(readFromCompression * sizeof(float));
        }

        return true;
    }

    public override void Serialize(Stream stream)
    {
        throw new NotImplementedException();
    }

    private const byte Version = 1;
    private const int Magic = 426698293;

    // public override unsafe void Serialize(Directory)
    // {
    //     Span<byte> buffer = stackalloc byte[6];
    //     var offset = 0;
    //     BinaryPrimitives.WriteInt32LittleEndian(buffer, Magic);
    //     offset += sizeof(int);
    //
    //     BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], Version);
    //     stream.Write(buffer);
    //
    //     foreach (var (key, ptr) in KeyIndex)
    //     {
    //         key
    //     }
    // }

    public override PairFeatureContainer<TKey, TId> Deserialize(Stream stream)
    {
        throw new NotImplementedException();
    }

    // public unsafe void SerializeBlock(IntPtr ptr, Stream stream)
    // {
    //     
    // }
}*/