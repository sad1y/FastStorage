using System;
using System.Collections.Generic;
using System.Linq;
using FeatureStorage.Memory;
using FeatureStorage.Storage;
using FluentAssertions;
using Xunit;

namespace FeatureStorage.Tests.Memory;

public class PinnedAllocatorTests
{
    [Fact]
    public void Allocate_SizeLargerThanBucketCapacity_ShouldBePossible()
    {
        using var allocator = new PinnedAllocator(32);
        var largePin = allocator.Allocate(64);
        largePin.Ptr.ToInt64().Should().NotBe(0);
    }

    [Fact]
    public void TryGet_ExistedAddress_ShouldReturnSamePint()
    {
        using var allocator = new PinnedAllocator(32);
        var expected = allocator.Allocate(8);

        allocator.TryGet(expected.Address, out var actual).Should().BeTrue();

        actual.Should().Be(expected);
    }

#if !DEBUG
    [Fact]
    public void TryGet_InvalidAddress_ShouldReturnFalse()
    {
        using var allocator = new PinnedAllocator(32);
        allocator.TryGet(-1, out _).Should().BeFalse();
    }
#endif

    [Fact]
    public void TryGet_AddressThatDoesNotExist_ShouldReturnFalse()
    {
        using var allocator = new PinnedAllocator(32);
        allocator.TryGet(1237182123, out _).Should().BeFalse();
    }

    [Fact]
    public void SerializeDeserialize_ShouldGiveSameData()
    {
        var data = Enumerable
            .Range(0, 64)
            .Select(f => (byte)f)
            .ToArray();

        using var allocator = new PinnedAllocator(32);

        var list = new List<PinnedMemory>();

        for (var i = 1; i < 8; i++)
        {
            var take = data.AsSpan(0, i * 8);
            var pin = allocator.Allocate(take.Length);
            take.CopyTo(pin.AsSpan(i * 8));
            list.Add(pin);
        }

        var fileSystem = new RamDirectory("/");

        PinnedAllocator.Serialize(fileSystem, allocator);

        var recreatedAllocator = PinnedAllocator.Deserialize(fileSystem);

        recreatedAllocator.BlockCapacity.Should().Be(allocator.BlockCapacity);

        unsafe
        {
            for (var i = 0; i < list.Count; i++)
            {
                recreatedAllocator.TryGet(list[i].Address, out var pin);
                var span = new ReadOnlySpan<byte>(pin, (i + 1) * 8);
                span.ToArray().Should().BeEquivalentTo(data.AsSpan(0, span.Length).ToArray());
            }
        }
    }
}