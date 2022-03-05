using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FeatureStorage.Memory;

public class RecycleRegionAllocator : MemoryAllocator, IDisposable
{
    private const uint MinRegionCapacity = 1024;

    private readonly uint _regionCapacity;

    private Region[] _regions;

    public RecycleRegionAllocator(uint regionCapacity = MinRegionCapacity)
    {
        if (regionCapacity < MinRegionCapacity)
            throw new ArgumentOutOfRangeException($"${nameof(regionCapacity)} should be equal or greater that {MinRegionCapacity}");

        _regionCapacity = regionCapacity;
        _regions = new Region[] { new(Marshal.AllocHGlobal((int)_regionCapacity)) };
    }

    public override IntPtr Allocate(int size)
    {
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "cannot allocate. size should be greater that zero");

        if (size > _regionCapacity)
            throw new ArgumentOutOfRangeException(nameof(size), "cannot allocate. size should be less that region capacity");

        return AllocateInternal(size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IntPtr AllocateInternal(int size)
    {
        for (var i = 0; i < _regions.Length; i++)
        {
            if (_regionCapacity - _regions[i].Offset > size)
                return _regions[i].Allocate(size);
        }

        Array.Resize(ref _regions, _regions.Length + 1);

        _regions[^1] = new Region(Marshal.AllocHGlobal((int)_regionCapacity));

        var ptr = _regions[^1].Allocate(size);

        return ptr;
    }

    public override void Free(IntPtr memory)
    {
        for (var i = 0; i < _regions.Length; i++)
        {
            var diff = memory.ToInt64() - _regions[i].Ptr.ToInt64();
            if (diff > 0 && _regionCapacity > diff)
                _regions[i].Free(memory);
        }
    }

    private enum Header : byte
    {
        InUse = 0,
        Returned = 1,
    }

    private struct Region
    {
        public IntPtr Ptr;
        public int Offset;
        private int _allocatedObjectCount;

        public Region(IntPtr ptr)
        {
            Ptr = ptr;
            Offset = 0;
            _allocatedObjectCount = 0;
        }

        public unsafe IntPtr Allocate(int size)
        {
            size += sizeof(Header);

            Unsafe.Write((Ptr + (int)Offset).ToPointer(), Header.InUse);
            var ptr = Ptr + (int)Offset + sizeof(Header);
            Offset += size;
            _allocatedObjectCount++;

            return ptr;
        }

        public unsafe void Free(IntPtr ptr)
        {
            ref var header = ref Unsafe.AsRef<Header>((ptr - sizeof(Header)).ToPointer());
            if (header == Header.InUse)
            {
                _allocatedObjectCount--;
                if (_allocatedObjectCount == 0)
                {
                    Offset = 0;
                }
            }

            header = Header.Returned;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) return;

        for (var i = 0; i < _regions.Rank; i++)
            Marshal.FreeHGlobal(_regions[i].Ptr);
    }

    public void Dispose() // Implement IDisposable
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}