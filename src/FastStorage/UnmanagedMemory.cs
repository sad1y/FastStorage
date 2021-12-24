using System.Runtime.InteropServices;

namespace FastStorage;

public class UnmanagedMemory : IDisposable
{
    private int _currentBlock = -1;
    private MemBlock[] _blocks = new MemBlock [128];
    
    private readonly int _blockSize;

    public UnmanagedMemory(int blockSize)
    {
        _blockSize = blockSize;
        AllocateNewMemBlock(blockSize);
    }

    /// <summary>
    /// allocate memory for requested size
    /// </summary>
    /// <param name="size"></param>
    /// <returns></returns>
    public IntPtr Allocate(int size)
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
    /// return last allocated memory
    /// </summary>
    /// <param name="ptr">rented memory ptr</param>
    /// <param name="size">rented memory size</param>
    /// <exception cref="IOException">throws IOException if trying to return non-last allocated memory</exception>
    public void Return(IntPtr ptr, int size)
    {
        var current = _blocks[_currentBlock].Return(size);

        if (ptr != current)
            throw new IOException("failed to return memory");
    }
    
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
    private void Dispose(bool disposing)
    {
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
                Allocated: {GetAllocatedMemory().InMegabytes().ToString("F")}mb, 
                Used: {GetMemoryUsage().InMegabytes().ToString("F")}mb";
    }
}