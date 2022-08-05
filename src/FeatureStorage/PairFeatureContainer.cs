using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FeatureStorage.Extensions;
using FeatureStorage.Memory;

namespace FeatureStorage;

public class PairFeatureContainer<TCodec, TIndex, TKey, TTag>
    where TKey : unmanaged
    where TTag : unmanaged
    where TCodec : IPairFeatureCodec<TTag>
    where TIndex : IIndex<TKey>
{
    private readonly ContiguousAllocator _allocator;

    private readonly RecycleRegionAllocator _tempAllocator = new((uint)16.Megabytes());
    private readonly int _featureCount;
    private readonly TIndex _keyIndex;
    private readonly TCodec _codec;

    public delegate void CreateBlock<in T>(ref PairFeatureBlockBuilder<TTag> builder, T state);

    public PairFeatureContainer(TCodec codec, TIndex index, int featureCount, int blockSize = 16 * 1024 * 1024)
    {
        _allocator = new ContiguousAllocator(blockSize);
        _codec = codec;
        _featureCount = featureCount;
        _keyIndex = index;
    }

    public void AddOrUpdate<TState>(TKey key, int capacity, CreateBlock<TState> blockCreator, TState state)
    {
        var blockBuilder = new PairFeatureBlockBuilder<TTag>(_tempAllocator, _featureCount, capacity);
        blockCreator(ref blockBuilder, state);
        var block = blockBuilder.ToBlock();

        try
        {
            // calc how many at most bytes needed
            var encoderBufferSize = blockBuilder.GetAllocatedSize() + _codec.MetaSize;
            var ptr = _allocator.Allocate(encoderBufferSize);
            unsafe
            {
                var buffer = new Span<byte>(ptr.ToPointer(), encoderBufferSize);
                if (!_codec.TryEncode(ref block, _featureCount, buffer[sizeof(int)..], out var written))
                    throw new IOException("cannot encode block");

                Unsafe.Write(ptr.ToPointer(), written);
                _keyIndex.Update(key, _allocator.Start.GetLongOffset(ptr));

                Debug.Assert(encoderBufferSize >= written + sizeof(int), "allocated buffer too small");
                // return unused memory 
                _allocator.Return(encoderBufferSize - (written + sizeof(int)));
            }
        }
        finally
        {
            blockBuilder.Release();
        }
    }

    public bool TryGet(TKey key, out PairFeatureBlock<TTag> featureBlock)
    {
        if (_keyIndex.TryGetValue(key, out var offset))
        {
            var ptr = _allocator.Start.MoveBy(offset);
            unsafe
            {
                var size = Unsafe.Read<int>(ptr.ToPointer());
                return _codec.TryDecode(new Span<byte>((ptr + sizeof(int)).ToPointer(), size), _featureCount, out featureBlock, out _);
            }
        }

        featureBlock = new PairFeatureBlock<TTag>();
        return false;
    }

    private const long Magic = 40267698293;

    private delegate void WriteKey(Span<byte> buffer, TKey val);

    public unsafe void Serialize(Stream stream)
    {
        Span<byte> meta = stackalloc byte[sizeof(int) + sizeof(int) + sizeof(long)];

        // write magic and version here
        BinaryPrimitives.WriteInt64LittleEndian(meta, Magic);
        BinaryPrimitives.WriteInt32LittleEndian(meta[8..], _codec.Stamp);
        BinaryPrimitives.WriteInt32LittleEndian(meta[12..], _keyIndex.Count);
        stream.Write(meta);

        var crc = Crc32.Append(meta);

        Span<byte> keyBuffer = stackalloc byte[sizeof(TKey)];

        WriteKey keyWriter = sizeof(TKey) switch
        {
            8 => (buffer, val) => BinaryPrimitives.WriteInt64LittleEndian(buffer, Unsafe.Read<long>(&val)),
            4 => (buffer, val) => BinaryPrimitives.WriteInt32LittleEndian(buffer, Unsafe.Read<int>(&val)),
            2 => (buffer, val) => BinaryPrimitives.WriteInt16LittleEndian(buffer, Unsafe.Read<short>(&val)),
            1 => (buffer, val) => buffer[0] = Unsafe.Read<byte>(&val),
            _ => throw new NotSupportedException()
        };

        foreach (var (key, offset) in _keyIndex)
        {
            var block = _allocator.Start.MoveBy(offset);
            var size = Unsafe.Read<int>(block.ToPointer());
            keyWriter(keyBuffer, key);
            stream.Write(keyBuffer);
            crc = Crc32.Append(keyBuffer, crc);

            var data = new Span<byte>(block.ToPointer(), size + sizeof(int)); // + size of size value 
            stream.Write(data);
            crc = Crc32.Append(data, crc);
        }

        Span<byte> crc32Buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(crc32Buffer, crc);
        stream.Write(crc32Buffer);
    }

    private delegate TKey ReadKey(Span<byte> buffer);

    public unsafe void Deserialize(Stream stream)
    {
        Span<byte> meta = stackalloc byte[sizeof(int) + sizeof(int) + sizeof(long)];

        // check magic and version
        if (stream.Read(meta) != meta.Length)
            throw new IOException("cannot read header");

        if (Magic != BinaryPrimitives.ReadInt64LittleEndian(meta))
            throw new IOException("header is invalid");

        if (BinaryPrimitives.ReadInt32LittleEndian(meta[8..]) != _codec.Stamp)
            throw new IOException("codec settings are different from what they were when data was serialized, it may leads to data corruption");

        var count = BinaryPrimitives.ReadInt32LittleEndian(meta[12..]);

        var crc = Crc32.Append(meta);

        ReadKey keyReader = sizeof(TKey) switch
        {
            8 => (buffer) =>
            {
                var lVal = BinaryPrimitives.ReadInt64LittleEndian(buffer);
                return Unsafe.Read<TKey>(&lVal);
            },
            4 => (buffer) =>
            {
                var iVal = BinaryPrimitives.ReadInt32LittleEndian(buffer);
                return Unsafe.Read<TKey>(&iVal);
            },
            2 => buffer =>
            {
                var sVal = BinaryPrimitives.ReadInt16LittleEndian(buffer);
                return Unsafe.Read<TKey>(&sVal);
            },
            1 => buffer =>
            {
                var bVal = buffer[0];
                return Unsafe.Read<TKey>(&bVal);
            },
            _ => throw new NotSupportedException()
        };

        Span<byte> keyAndSize = stackalloc byte[sizeof(TKey) + sizeof(int)];

        for (var i = 0; count > i; i++)
        {
            var read = stream.Read(keyAndSize);
            if (read != keyAndSize.Length)
                throw new IOException("cannot read record");

            crc = Crc32.Append(keyAndSize, crc);

            var key = keyReader(keyAndSize);
            var size = BinaryPrimitives.ReadInt32LittleEndian(keyAndSize[sizeof(TKey)..]);

            var ptr = _allocator.Allocate(size + sizeof(int));
            var data = new Span<byte>(ptr.ToPointer(), size + sizeof(int));

            BinaryPrimitives.WriteInt32LittleEndian(data, size);

            data = data[sizeof(int)..]; // remove size from buffer

            if (stream.Read(data) != size)
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
}