using System;
using FeatureStorage.Memory;
using Xunit;

namespace FeatureStorage.Tests.Memory;

public class RecycleRegionAllocatorTests
{
    [Fact]
    public void DoubleFree_ShouldNotThrowOrCorruptMemory()
    {
        using var allocator = new RecycleRegionAllocator();

        var ptr1 = allocator.Allocate(300);
        var ptr2 = allocator.Allocate(200);
        
        allocator.Free(ptr1);
        allocator.Free(ptr1);
    }
    
    [Fact]
    public void AllocateAndFree_ShouldReuseMemoryRegion()
    {
        using var allocator = new RecycleRegionAllocator();

        var ptr1 = allocator.Allocate(1000);
        allocator.Allocate(100); // should allocate new region
        
        allocator.Free(ptr1);
        
        var ptr2 = allocator.Allocate(50);
        
        Assert.Equal(ptr1, ptr2);
    }
    
    [Fact]
    public void Free_PtrWhichDoesntBelongsToAllocator()
    {
        using var allocator = new RecycleRegionAllocator();

        allocator.Free(IntPtr.Zero);
    }
}