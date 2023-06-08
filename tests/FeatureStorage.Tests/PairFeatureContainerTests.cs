using FeatureStorage.Memory;
using FeatureStorage.Storage;
using FluentAssertions;
using Xunit;

namespace FeatureStorage.Tests;
public class PairFeatureContainerTests
{
    [Fact]
    public void TryGetShouldReturnBlockIfExists()
    {
        var storage = new PairFeatureContainer<SimpleCodec, SimpleIndex, string, long>(new SimpleCodec(), new SimpleIndex(), 5);
        storage.AddOrUpdate("1235s982013sasd0", 3, (ref PairFeatureBlockBuilder<long> builder, object state) =>
        {
            builder.AddFeatures(10, new float[] { 1, 2, 3, 4, 5 });
            builder.AddFeatures(20, new float[] { 2, 4, 6, 8, 10 });
            builder.AddFeatures(30, new float[] { 3, 6, 9, 12, 15 });
        }, null);

        using var allocator = new RecycleRegionAllocator();
        
        storage.TryGet("1235s982013sasd0", allocator, out var block).Should().BeTrue();
        block.Count.Should().Be(3);
        block.GetIds().ToArray().Should().BeEquivalentTo(new long[] { 10, 20, 30 });
        block.GetFeatureMatrix().ToArray().Should().BeEquivalentTo(new float[] { 1, 2, 3, 4, 5, 2, 4, 6, 8, 10, 3, 6, 9, 12, 15 });
        
    }

    [Fact]
    public void TryGetShouldNotReturnBlockIfItDoesNotExists()
    {
        var storage = new PairFeatureContainer<SimpleCodec, SimpleIndex, string, long>(new SimpleCodec(), new SimpleIndex(), 5);
        storage.TryGet("1235s982013sasd0", null, out _).Should().BeFalse();
    }

    [Fact]
    public void SerializeDeserializeShouldReturnSameData()
    {
        const string utf8Key = "接受那个";
        using var storage = new PairFeatureContainer<SimpleCodec, SimpleIndex, string, long>(new SimpleCodec(), new SimpleIndex(), 5);
        
        storage.AddOrUpdate(utf8Key, 3, (ref PairFeatureBlockBuilder<long> builder, object? _) =>
        {
            builder.AddFeatures(10, new float[] { 1, 2, 3, 4, 5 });
            builder.AddFeatures(20, new float[] { 2, 4, 6, 8, 10 });
            builder.AddFeatures(30, new float[] { 3, 6, 9, 12, 15 });
        }, null);

        var root = new RamDirectory("/");
        storage.Serialize(root);

        using var recoveredStorage = new PairFeatureContainer<SimpleCodec, SimpleIndex, string, long>(new SimpleCodec(), new SimpleIndex(), 5);
        recoveredStorage.Deserialize(root);

        using var allocator = new RecycleRegionAllocator();

        recoveredStorage.TryGet(utf8Key, allocator, out var block).Should().BeTrue();
        block.Count.Should().Be(3);
        block.GetIds().ToArray().Should().BeEquivalentTo(new long[] { 10, 20, 30 });
        block.GetFeatureMatrix().ToArray().Should().BeEquivalentTo(new float[] { 1, 2, 3, 4, 5, 2, 4, 6, 8, 10, 3, 6, 9, 12, 15 });
    }
}