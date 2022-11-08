using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FeatureStorage.Extensions;
using FeatureStorage.Memory;
using FeatureStorage.Storage;

namespace FeatureStorage;

public class PairFeatureContainer<TCodec, TIndex, TKey, TId> : IDisposable
    where TId : unmanaged
    where TCodec : IPairFeatureCodec<TId>
    where TIndex : IIndex<TKey>
{
    private PinnedAllocator _allocator;

    private readonly RecycleRegionAllocator _tempAllocator = new((uint)16.Megabytes());
    private readonly int _featureCount;
    private readonly TIndex _keyIndex;
    private readonly TCodec _codec;

    public delegate void CreateBlock<in T>(ref PairFeatureBlockBuilder<TId> builder, T state);

    public PairFeatureContainer(TCodec codec, TIndex index, int featureCount, int blockSize = 16 * 1024 * 1024)
    {
        _allocator = new PinnedAllocator(blockSize);
        _codec = codec;
        _featureCount = featureCount;
        _keyIndex = index;
    }

    private const int HeaderSize = sizeof(int) * 2; // header gonna contains size int bytes that occupied by block plus count of entries  

    public void AddOrUpdate<TState>(TKey key, int capacity, CreateBlock<TState> blockCreator, TState state)
    {
        var blockBuilder = new PairFeatureBlockBuilder<TId>(_tempAllocator, _featureCount, capacity);
        blockCreator(ref blockBuilder, state);
        var block = blockBuilder.ToBlock();

        try
        {
            // calc how many at most bytes needed
            var atMostBytesRequired = block.GetAllocatedSize() + _codec.MetaSize;
            var pin = _allocator.Allocate(atMostBytesRequired);
            unsafe
            {
                var buffer = new Span<byte>(pin, atMostBytesRequired);

                if (!_codec.TryEncode(ref block, buffer[HeaderSize..], out var written))
                    throw new IOException("cannot encode block");

                Unsafe.Write(pin, written);
                Unsafe.Write((pin + sizeof(int)).ToPointer(), block.Count);
                _keyIndex.Update(key, pin.Address);

                Debug.Assert(atMostBytesRequired >= written + HeaderSize, "allocated buffer too small");
                // return unused memory 
                _allocator.Return(atMostBytesRequired - (written + HeaderSize));
            }
        }
        finally
        {
            block.Release();
        }
    }

    /// <summary>
    /// Attempts to get <paramref name="pairFeatureBlock"/> by <paramref name="key"/> and deserialize it
    /// IMPORTANT. If operation return <c>true</c> then you are responsible for returning memory by calling <c>Release</c> method on <paramref name="pairFeatureBlock"/> 
    /// <example>
    /// <code>
    /// if(container.TryGet(key, allocator, out var pairFeatureBlock)) {
    ///     try {
    ///         ....
    ///     }
    ///     finally {
    ///         pairFeatureBlock.Release();
    ///     }
    /// }
    /// </code>
    /// </example> 
    /// </summary>
    /// <param name="key">Object that identify <paramref name="pairFeatureBlock"/></param>
    /// <param name="allocator">Used as temporary storage for <paramref name="pairFeatureBlock"/>. You could try <c>RecycleRegionAllocator</c></param> 
    /// <param name="pairFeatureBlock">Reference to deserialized <c>PairFeatureBlock</c> on <paramref name="pairFeatureBlock"/></param> 
    /// <returns>true if <paramref name="pairFeatureBlock"/> exists</returns>
    public bool TryGet(TKey key, MemoryAllocator allocator, out PairFeatureBlock<TId> pairFeatureBlock)
    {
        if (TryGet(key, out var count, out var span))
        {
            pairFeatureBlock = new PairFeatureBlock<TId>(allocator, count, _featureCount);
            return _codec.TryDecode(span, ref pairFeatureBlock, out _);
        }

        pairFeatureBlock = new PairFeatureBlock<TId>();

        return false;
    }

    /// <summary>
    /// Attempts to get raw <paramref name="pairFeatureBlock"/> data by <paramref name="key"/> 
    /// </summary>
    /// <param name="key">Object that identify <paramref name="pairFeatureBlock"/></param>
    /// <param name="count">Contains number of elements in that <paramref name="pairFeatureBlock"/></param>
    /// <param name="pairFeatureBlock">Reference to raw <c>PairFeatureBlock</c> data</param> 
    /// <returns>true if <paramref name="pairFeatureBlock"/> exists</returns>
    public bool TryGet(TKey key, out int count, out ReadOnlySpan<byte> pairFeatureBlock)
    {
        if (_keyIndex.TryGetValue(key, out var address) && _allocator.TryGet(address, out var pin))
        {
            unsafe
            {
                var size = Unsafe.Read<int>(pin);
                count = Unsafe.Read<int>((pin + sizeof(int)).ToPointer());
                pairFeatureBlock = new ReadOnlySpan<byte>((pin + HeaderSize).ToPointer(), size);
            }

            return true;
        }

        pairFeatureBlock = new ReadOnlySpan<byte>();
        count = 0;
        return false;
    }

    private const long Magic = 0xDEADF00D;

    private const string MetaFileName = "meta";
    private const string ContainerDirectoryName = "data";
    private const string IndexDirectoryName = "index";

    public unsafe void Serialize(IDirectory root)
    {
        Span<byte> meta = stackalloc byte[sizeof(int) + sizeof(long)];

        var metaFile = root.CreateFile(MetaFileName);
        using var containerStream = metaFile.OpenWrite();

        // write magic and version here
        BinaryPrimitives.WriteInt64LittleEndian(meta, Magic);
        BinaryPrimitives.WriteInt32LittleEndian(meta[8..], _codec.Stamp);
        containerStream.Write(meta);

        Span<byte> crcBuffer = stackalloc byte[sizeof(int)];
        var crc = Crc32.Append(meta);
        BinaryPrimitives.WriteUInt32LittleEndian(crcBuffer, crc);
        containerStream.Write(crcBuffer);
        containerStream.Flush();

        _keyIndex.Serialize(root.CreateDirectory(IndexDirectoryName));
        PinnedAllocator.Serialize(root.CreateDirectory(ContainerDirectoryName), _allocator);
    }

    public unsafe void Deserialize(IDirectory root)
    {
        Span<byte> meta = stackalloc byte[sizeof(int) + sizeof(long)];

        var metaFile = root.GetFile(MetaFileName);
        using var metaStream = metaFile.OpenRead();

        // check magic and version
        if (metaStream.Read(meta) != meta.Length)
            throw new IOException("Cannot read header.");

        if (Magic != BinaryPrimitives.ReadInt64LittleEndian(meta))
            throw new IOException("Header is invalid.");

        if (BinaryPrimitives.ReadInt32LittleEndian(meta[8..]) != _codec.Stamp)
            throw new IOException(
                "Codec settings are different from what they were when data was serialized, it may leads to data corruption.");

        var crc = Crc32.Append(meta);

        Span<byte> crcBuffer = stackalloc byte[sizeof(uint)];
        if (metaStream.Read(crcBuffer) != crcBuffer.Length)
            throw new IOException("Cannot read meta file crc.");

        if (crc != BinaryPrimitives.ReadUInt32LittleEndian(crcBuffer))
            throw new IOException("Invalid meta file crc.");

        var indexDir = root.GetDirectory(IndexDirectoryName);

        if (indexDir == null)
            throw new IOException("Cannot find `Index` directory");
        _keyIndex.Deserialize(indexDir);

        var dataDir = root.GetDirectory(ContainerDirectoryName);
        if (dataDir == null)
            throw new IOException("Cannot find `Data` directory");
        
        _allocator = PinnedAllocator.Deserialize(dataDir);
    }

    public void Dispose()
    {
        _allocator.Dispose();
        _tempAllocator.Dispose();
    }
}