using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FeatureStorage.Extensions;

namespace FeatureStorage.Memory;

public class ContiguousAllocator : MemoryAllocator, IDisposable
{
    private int _currentBlock = -1;
    private MemBlock[] _blocks = new MemBlock [128];

    private readonly int _blockSize;

    public ContiguousAllocator(int blockSize)
    {
        _blockSize = blockSize;
        AllocateNewMemBlock(blockSize);
    }

    public override IntPtr Allocate(int size)
    {
        while (true)
        {
            for (var i = _currentBlock; i < _blocks.Length; i++)
            {
                if (_blocks[_currentBlock].FreeSpace < size)
                {
                    AllocateNewMemBlock(Math.Max(_blockSize, size));
                    continue;
                }

                return _blocks[_currentBlock].Reserve(size);
            }

            Array.Resize(ref _blocks, (int)(_blocks.Length * 1.25));
        }
    }

    /// <summary>
    /// do nothing
    /// </summary>
    /// <param name="memory"></param>
    public override void Free(IntPtr memory)
    {
    }

    /// <summary>
    /// mark memory as unused in last allocated memory block
    /// </summary>
    /// <param name="size">bytes to return</param>
    public void Return(int size)
    {
        _blocks[_currentBlock].Return(size);
    }

    /// <summary>
    /// reset memory blocks usage
    /// </summary>
    public void Reset()
    {
        for (var i = 0; i < _blocks.Length; i++)
        {
            _blocks[i].Reset();
        }
    }

    public IntPtr Start => _blocks[0].Ptr;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AllocateNewMemBlock(int size)
    {
        var ptr = Marshal.AllocHGlobal(size);
        _blocks[++_currentBlock] = new MemBlock(ptr, size);
    }

    private struct MemBlock
    {
        private int _offset;
        private readonly int _capacity;
        public readonly IntPtr Ptr;
        public int FreeSpace => _capacity - _offset;
        public int Size => _offset;
        public int Capacity => _capacity;

        public void Reset()
        {
            _offset = 0;
        }

        public bool IsInitialized() => Ptr != IntPtr.Zero && _capacity > 0;

        public MemBlock(IntPtr ptr, int capacity)
        {
            _offset = 0;
            _capacity = capacity;
            Ptr = ptr;
        }

        public IntPtr Reserve(int size)
        {
            var ptr = Ptr + _offset;
            _offset += size;
            return ptr;
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

    public override string ToString()
    {
        return @$"Blocks: {_currentBlock + 1}, 
                Allocated: {GetAllocatedMemory().InMegabytes():F}mb, 
                Used: {GetMemoryUsage().InMegabytes():F}mb";
    }
}