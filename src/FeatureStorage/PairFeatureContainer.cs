using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FeatureStorage.Extensions;
using FeatureStorage.Memory;

namespace FeatureStorage;

public class PairFeatureContainer<TCodec, TIndex, TKey, TId> : IDisposable
    where TId : unmanaged
    where TCodec : IPairFeatureCodec<TId>
    where TIndex : IIndex<TKey>
{
    private readonly ContiguousAllocator _allocator;

    private readonly RecycleRegionAllocator _tempAllocator = new((uint)16.Megabytes());
    private readonly int _featureCount;
    private readonly TIndex _keyIndex;
    private readonly TCodec _codec;

    public delegate void CreateBlock<in T>(ref PairFeatureBlockBuilder<TId> builder, T state);

    public PairFeatureContainer(TCodec codec, TIndex index, int featureCount, int blockSize = 16 * 1024 * 1024)
    {
        _allocator = new ContiguousAllocator(blockSize);
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
            var ptr = _allocator.Allocate(atMostBytesRequired);
            unsafe
            {
                var buffer = new Span<byte>(ptr.ToPointer(), atMostBytesRequired);

                if (!_codec.TryEncode(ref block, buffer[HeaderSize..], out var written))
                    throw new IOException("cannot encode block");

                Unsafe.Write(ptr.ToPointer(), written);
                Unsafe.Write((ptr + sizeof(int)).ToPointer(), block.Count);
                _keyIndex.Update(key, _allocator.Start.GetLongOffset(ptr));

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
        if (_keyIndex.TryGetValue(key, out var offset))
        {
            var ptr = _allocator.Start.MoveBy(offset);
            unsafe
            {
                var size = Unsafe.Read<int>(ptr.ToPointer());
                var count = Unsafe.Read<int>((ptr + sizeof(int)).ToPointer());
                pairFeatureBlock = new PairFeatureBlock<TId>(allocator, count, _featureCount);
                return _codec.TryDecode(new Span<byte>((ptr + HeaderSize).ToPointer(), size), ref pairFeatureBlock, out _);
            }
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
        if (_keyIndex.TryGetValue(key, out var offset))
        {
            var ptr = _allocator.Start.MoveBy(offset);
            unsafe
            {
                var size = Unsafe.Read<int>(ptr.ToPointer());
                count = Unsafe.Read<int>((ptr + sizeof(int)).ToPointer());
                pairFeatureBlock = new ReadOnlySpan<byte>((ptr + HeaderSize).ToPointer(), size);
            }
        }

        pairFeatureBlock = new ReadOnlySpan<byte>();
        count = 0;
        return false;
    }

    private const long Magic = 0xDEADF00D;

    private const int MaxKeySize = 1024;

    public unsafe void Serialize(Stream stream)
    {
        Span<byte> meta = stackalloc byte[sizeof(int) + sizeof(int) + sizeof(long)];

        // write magic and version here
        BinaryPrimitives.WriteInt64LittleEndian(meta, Magic);
        BinaryPrimitives.WriteInt32LittleEndian(meta[8..], _codec.Stamp);
        BinaryPrimitives.WriteInt32LittleEndian(meta[12..], _keyIndex.Count);
        stream.Write(meta);

        var crc = Crc32.Append(meta);

        Span<byte> keyBuffer = stackalloc byte[MaxKeySize];

        foreach (var (key, offset) in _keyIndex)
        {
            var block = _allocator.Start.MoveBy(offset);
            var size = Unsafe.Read<int>(block.ToPointer());

            var keySpan = keyBuffer[sizeof(ushort)..];

            if (!_keyIndex.TrySerialize(key, keySpan, out var keySize))
                throw new IOException($"cannot write key `{key}`");

            BinaryPrimitives.WriteUInt16LittleEndian(keyBuffer, (ushort)keySize);

            keySpan = keyBuffer[..(keySize + sizeof(ushort))];

            stream.Write(keySpan);
            crc = Crc32.Append(keySpan, crc);

            var data = new Span<byte>(block.ToPointer(), size + HeaderSize); // + size of size value 
            stream.Write(data);
            crc = Crc32.Append(data, crc);
        }

        Span<byte> crc32Buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(crc32Buffer, crc);
        stream.Write(crc32Buffer);
    }

    public unsafe void Deserialize(Stream stream)
    {
        Span<byte> meta = stackalloc byte[sizeof(int) + sizeof(int) + sizeof(long)];

        // check magic and version
        if (stream.Read(meta) != meta.Length)
            throw new IOException("cannot read header");

        if (Magic != BinaryPrimitives.ReadInt64LittleEndian(meta))
            throw new IOException("header is invalid");

        if (BinaryPrimitives.ReadInt32LittleEndian(meta[8..]) != _codec.Stamp)
            throw new IOException(
                "codec settings are different from what they were when data was serialized, it may leads to data corruption");

        var count = BinaryPrimitives.ReadInt32LittleEndian(meta[12..]);

        var crc = Crc32.Append(meta);

        Span<byte> headerBuffer = stackalloc byte[MaxKeySize + sizeof(int)];

        for (var i = 0; count > i; i++)
        {
            var headerSizeBuffer = headerBuffer[..sizeof(ushort)];
            var read = stream.Read(headerSizeBuffer);
            if (read != headerSizeBuffer.Length)
                throw new IOException("cannot read record header size");

            crc = Crc32.Append(headerSizeBuffer, crc);

            var headerSize = BinaryPrimitives.ReadUInt16LittleEndian(headerSizeBuffer);

            var header = headerBuffer.Slice(sizeof(ushort), headerSize);
            read = stream.Read(header);
            if (read != header.Length)
                throw new IOException("cannot read record header");

            if (!_keyIndex.TryDeserialize(header, out var key, out var keySize))
                throw new IOException($"cannot read key");

            crc = Crc32.Append(header[..keySize], crc);

            var bodySize = headerBuffer[..sizeof(int)];

            read = stream.Read(bodySize);

            if (read != bodySize.Length)
                throw new IOException("cannot read record body size");

            crc = Crc32.Append(bodySize, crc);

            var size = BinaryPrimitives.ReadInt32LittleEndian(bodySize);

            var ptr = _allocator.Allocate(size + HeaderSize);
            var data = new Span<byte>(ptr.ToPointer(), size + HeaderSize);

            BinaryPrimitives.WriteInt32LittleEndian(data, size);

            data = data[sizeof(int)..]; // remove size from buffer

            if (stream.Read(data) != data.Length)
                throw new IOException("cannot copy record data");

            crc = Crc32.Append(data, crc);
            _keyIndex.Update(key, _allocator.Start.GetLongOffset(ptr));
        }

        Span<byte> crc32Buffer = stackalloc byte[sizeof(uint)];

        if (stream.Read(crc32Buffer) != crc32Buffer.Length)
            throw new IOException("cannot read crc32 value");

        if (crc != BinaryPrimitives.ReadUInt32LittleEndian(crc32Buffer))
            throw new IOException("crc32 check failed");

        // check that was last data in stream
        if (stream.Read(crc32Buffer) != 0)
            throw new IOException("data malformed");
    }

    public void Dispose()
    {
        _allocator.Dispose();
        _tempAllocator.Dispose();
    }
}