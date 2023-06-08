using System.IO;
using FluentAssertions;
using Xunit;

namespace FeatureStorage.Tests;

public class PairFeatureContainerTests
{
    [Fact]
    public void TryGetShouldReturnBlockIfExists()
    {
        var storage =
            new PairFeatureContainer<SimpleCodec, SimpleIndex<long>, long, long>(new SimpleCodec(), new SimpleIndex<long>(), 5);
        storage.AddOrUpdate(30, 3, (ref PairFeatureBlockBuilder<long> builder, object? _) =>
        {
            builder.AddFeatures(10, new float[] { 1, 2, 3, 4, 5 });
            builder.AddFeatures(20, new float[] { 2, 4, 6, 8, 10 });
            builder.AddFeatures(30, new float[] { 3, 6, 9, 12, 15 });
        }, null);

        storage.TryGet(30, out var block).Should().BeTrue();
        block.Count.Should().Be(3);
        block.GetIds().ToArray().Should().BeEquivalentTo(new long[] { 10, 20, 30 });
        block.GetFeatureMatrix().ToArray().Should()
            .BeEquivalentTo(new float[] { 1, 2, 3, 4, 5, 2, 4, 6, 8, 10, 3, 6, 9, 12, 15 });
    }

    [Fact]
    public void TryGetShouldNotReturnBlockIfItDoesNotExists()
    {
        var storage =
            new PairFeatureContainer<SimpleCodec, SimpleIndex<long>, long, long>(new SimpleCodec(), new SimpleIndex<long>(), 5);
        storage.TryGet(30, out _).Should().BeFalse();
    }

    [Fact]
    public void SerializeDeserializeShouldReturnSameData()
    {
        var storage =
            new PairFeatureContainer<SimpleCodec, SimpleIndex<long>, long, long>(new SimpleCodec(), new SimpleIndex<long>(), 5);
        storage.AddOrUpdate(30, 3, (ref PairFeatureBlockBuilder<long> builder, object? _) =>
        {
            builder.AddFeatures(10, new float[] { 1, 2, 3, 4, 5 });
            builder.AddFeatures(20, new float[] { 2, 4, 6, 8, 10 });
            builder.AddFeatures(30, new float[] { 3, 6, 9, 12, 15 });
        }, null);

        using var mem = new MemoryStream();
        storage.Serialize(mem);
        mem.Position = 0;

        var recoveredStorage =
            new PairFeatureContainer<SimpleCodec, SimpleIndex<long>, long, long>(new SimpleCodec(), new SimpleIndex<long>(), 5);
        recoveredStorage.Deserialize(mem);

        recoveredStorage.TryGet(30, out var block).Should().BeTrue();
        block.Count.Should().Be(3);
        block.GetIds().ToArray().Should().BeEquivalentTo(new long[] { 10, 20, 30 });
        block.GetFeatureMatrix().ToArray().Should()
            .BeEquivalentTo(new float[] { 1, 2, 3, 4, 5, 2, 4, 6, 8, 10, 3, 6, 9, 12, 15 });
    }
}