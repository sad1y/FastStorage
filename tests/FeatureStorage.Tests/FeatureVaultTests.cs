using Moq;
using Xunit;

namespace FeatureStorage.Tests;

public class FeatureVaultTests
{
    [Fact]
    public void TryGetShouldReturnBlockIfExists()
    {
        var storage = new PairFeatureVault<long, long>(5, Mock.Of<PairFeatureCodec<long>>());
        storage.AddOrUpdate(30, 3, (ref PairFeatureBlockBuilder<long> builder) =>
        {
            builder.AddFeatures(10, new float[] { 1, 2, 3, 4, 5 });
            builder.AddFeatures(20, new float[] { 2, 4, 6, 8, 10 });
            builder.AddFeatures(30, new float[] { 3, 6, 9, 12, 15 });
        });

        Assert.True(storage.TryGet(30, out var block));
        Assert.Equal(3, block.Count);
        Assert.Equal(new long[] { 10, 20, 30 }, block.GetTag().ToArray());
        Assert.Equal(new float[] { 1, 2, 3, 4, 5, 2, 4, 6, 8, 10, 3, 6, 9, 12, 15 }, block.GetFeatureMatrix().ToArray());
    }
}