using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FeatureStorage.Extensions;
using FeatureStorage.Storage;

namespace FeatureStorage.Memory;

public class PinnedAllocator : IDisposable
{
    private int _currentBlock = -1;
    private MemBlock[] _blocks = new MemBlock [8];

    private readonly int _blockCapacity;

    public object BlockCapacity => _blockCapacity;

    public PinnedAllocator(int blockCapacity)
    {
        _blockCapacity = blockCapacity;
        AllocateNewMemBlock(blockCapacity);
    }

    private PinnedAllocator(int blockCapacity, MemBlock[] blocks)
    {
        _blockCapacity = blockCapacity;
        _blocks = blocks;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ToAddress(long block, int offset)
    {
        var address = block << 32;
        return address | (uint)(offset & 0x7FFFFFFF);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int blockIndex, int offset) FromAddress(long address)
    {
        var blockIndex = address >> 32;
        var offset = address & 0x7FFFFFFF;

        Debug.Assert(offset >= 0, "offset >= 0 check fail");
        Debug.Assert(blockIndex >= 0, "blockIndex >= 0 check fail");

        return ((int)blockIndex, (int)offset);
    }

    public PinnedMemory Allocate(int size)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));

        while (true)
        {
            for (var i = _currentBlock; i < _blocks.Length; i++)
            {
                if (!_blocks[_currentBlock].CanAllocate(size))
                {
                    if(_blocks.Length - 1 == _currentBlock)
                        break;
                    AllocateNewMemBlock(Math.Max(_blockCapacity, size));
                    continue;
                }

                var (ptr, offset) = _blocks[_currentBlock].Reserve(size);
                return new PinnedMemory(ptr, ToAddress(_currentBlock, offset));
            }

            Array.Resize(ref _blocks, _blocks.Length + 8);
        }
    }

    /// <summary>
    /// give pinned memory by address 
    /// </summary>
    /// <returns>true if memory at the given address is available, otherwise false</returns>
    public bool TryGet(long address, out PinnedMemory pinnedMemory)
    {
        var (index, offset) = FromAddress(address);

        if (index >= 0 && _blocks.Length > index && offset >= 0 && _blocks[index].Capacity > offset)
        {
            var ptr = _blocks[index].Ptr;
            pinnedMemory = new PinnedMemory(ptr + offset, address);

            return true;
        }

        pinnedMemory = new PinnedMemory();
        return false;
    }

    /// <summary>
    /// mark memory as unused in last allocated memory block
    /// </summary>
    /// <param name="size">bytes to return</param>
    public void Return(int size)
    {
        _blocks[_currentBlock].Return(size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AllocateNewMemBlock(int size)
    {
        var ptr = Marshal.AllocHGlobal(size);
        _blocks[++_currentBlock] = new MemBlock(ptr, size);
    }

    private const string SerializeBlockPrefix = "block_";
    private const string SerializeBlockExt = ".bin";
    private const string SerializeMetaFileName = "meta";

    public static void Serialize(IDirectory directory, PinnedAllocator allocator)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int) + sizeof(uint)];

        var metaFile = directory.CreateFile(SerializeMetaFileName);
        BinaryPrimitives.WriteInt32LittleEndian(buffer, allocator._blockCapacity);

        var crc = buffer[..sizeof(int)].Crc32();
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[sizeof(int)..], crc);
        using var metaStream = metaFile.OpenWrite();
        metaStream.Write(buffer);
        metaStream.Flush();

        for (var i = 0; i < allocator._blocks.Length; i++)
        {
            var block = allocator._blocks[i];
            var file = directory.CreateFile($"{SerializeBlockPrefix}{i}{SerializeBlockExt}");

            BinaryPrimitives.WriteInt32LittleEndian(buffer, block.Size);

            using var stream = file.OpenWrite();

            stream.Write(buffer);

            unsafe
            {
                var blob = new ReadOnlySpan<byte>(block.Ptr.ToPointer(), block.Size);
                stream.Write(blob);

                crc = blob.Crc32();
                var crcSpan = buffer[sizeof(uint)..];
                BinaryPrimitives.WriteUInt32LittleEndian(crcSpan, crc);
                stream.Write(crcSpan);
            }

            stream.Flush();
        }
    }

    public static PinnedAllocator Deserialize(IDirectory directory)
    {
        var files = directory.GetFiles().ToList();
        var blocks = new MemBlock[files.Count];

        var metaFile = files.FirstOrDefault(f => f.Name == SerializeMetaFileName);

        if (metaFile is null)
            throw new IOException("Cannot find meta file");

        Span<byte> buffer = stackalloc byte[sizeof(int) + sizeof(uint)];

        using var metaStream = metaFile.OpenRead();

        if (metaStream.Read(buffer) != buffer.Length)
            throw new IOException($"Cannot read meta file");

        var blockSize = BinaryPrimitives.ReadInt32LittleEndian(buffer);
        var crc = BinaryPrimitives.ReadUInt32LittleEndian(buffer[sizeof(int)..]);

        if (crc != buffer[..sizeof(int)].Crc32())
            throw new IOException("Corrupted meta file");

        for (var i = 0; i < files.Count; i++)
        {
            if (files[i].Name == SerializeMetaFileName)
                continue;

            var file = files[i];
            
            var suffix = files[i].Name.AsSpan(
                SerializeBlockPrefix.Length,
                files[i].Name.Length - (SerializeBlockExt.Length + SerializeBlockPrefix.Length));

            if (!int.TryParse(suffix, out var index))
                throw new InvalidCastException($"Cannot parse file `{file.Name}` suffix");

            using var stream = file.OpenRead();

            if (stream.Read(buffer) != buffer.Length)
                throw new IOException($"Cannot read header from `{file.Name}`");

            var size = BinaryPrimitives.ReadInt32LittleEndian(buffer);
            var ptr = Marshal.AllocHGlobal(size);

            unsafe
            {
                var span = new Span<byte>(ptr.ToPointer(), size);

                var left = size;
                while (left != 0)
                {
                    var read = stream.Read(span[..left]);

                    if (read == 0)
                        throw new IOException($"Stream does not contains enough data. File: `{file.Name}`");

                    left -= read;
                }

                if (stream.Read(buffer[..sizeof(int)]) != sizeof(int))
                    throw new IOException($"Cannot read crc32 from stream. File: `{file.Name}`");

                if (span.Crc32() != BinaryPrimitives.ReadUInt32LittleEndian(buffer[..sizeof(int)]))
                    throw new IOException($"Invalid crc32. File: `{file.Name}`");

                blocks[index] = new MemBlock(ptr, size);
            }
        }

        return new PinnedAllocator(blockSize, blocks);
    }

    private struct MemBlock
    {
        private int _offset;
        private readonly int _capacity;
        public readonly IntPtr Ptr;
        public int FreeSpace => _capacity - _offset;
        public int Size => _offset;
        public int Capacity => _capacity;

        public bool IsInitialized() => Ptr != IntPtr.Zero;
        public bool CanAllocate(int size) => _offset + size <= _capacity;

        public MemBlock(IntPtr ptr, int capacity)
        {
            _offset = 0;
            _capacity = capacity;
            Ptr = ptr;
        }

        public (IntPtr ptr, int offset) Reserve(int size)
        {
            var offset = _offset;
            var ptr = Ptr + _offset;
            _offset += size;

            return (ptr, offset);
        }

        public IntPtr Return(int size)
        {
            _offset -= size;
            return Ptr + _offset;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        for (var i = 0; i < _blocks.Length; i++)
        {
            if (_blocks[i].IsInitialized())
            {
                Marshal.FreeHGlobal(_blocks[i].Ptr);
            }
        }
    }

    public long GetAllocatedMemory()
    {
        var sum = 0;

        for (var i = 0; _blocks[i].IsInitialized(); i++)
        {
            sum += _blocks[i].Capacity;
        }

        return sum;
    }

    public long GetMemoryUsage()
    {
        var sum = 0;

        for (var i = 0; _blocks[i].IsInitialized(); i++)
        {
            sum += _blocks[i].Size;
        }

        return sum;
    }

    public override string ToString() =>
        @$"Blocks: {_currentBlock + 1}, Allocated: {GetAllocatedMemory().InMegabytes():F}mb, Used: {GetMemoryUsage().InMegabytes():F}mb";
}