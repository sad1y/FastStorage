using System;
using FeatureStorage.Memory;
using FluentAssertions;
using Xunit;

namespace FeatureStorage.Tests;

public class ZfpCodecTests
{
    [Fact]
    public unsafe void EncodeDecode_Should()
    {
        using var allocator = new RecycleRegionAllocator(4096);
        var codec = new ZfpCodec<long>(allocator, 1e-5);

        const int featureCount = 100;
        const int count = 10;

        var builder = new PairFeatureBlockBuilder<long>(allocator, featureCount, count);
        var rng = new Random(1509872513);
        Span<float> features = stackalloc float[featureCount];
        for (var i = 0; count > i; i++)
        {
            for (var j = 0; j < featureCount; j++) features[j] = rng.NextSingle();
            builder.AddFeatures(rng.NextInt64(), features);
        }
        
        var featureBlock = builder.ToBlock();

        var encodedStreamPtr = allocator.Allocate(builder.GetAllocatedSize());
        Assert.True(codec.TryEncode(ref featureBlock, encodedStreamPtr, out var written));
        Assert.True(codec.TryDecode(encodedStreamPtr, out var decodedBlock, out var read));

        Assert.Equal(featureBlock.Count, decodedBlock.Count);
        Assert.Equal(featureBlock.FeatureCount, decodedBlock.FeatureCount);
        Assert.Equal(featureBlock.GetMeta().ToArray(), decodedBlock.GetMeta().ToArray());

        var originalSpan = featureBlock.GetFeatureMatrix();
        var decodedSpan = decodedBlock.GetFeatureMatrix();
        originalSpan.Length.Should().Be(decodedSpan.Length);
        
        for (var i = 0; i < originalSpan.Length; i++) 
            originalSpan[i].Should().BeApproximately(decodedSpan[i], (float)1e-5);
    }
}