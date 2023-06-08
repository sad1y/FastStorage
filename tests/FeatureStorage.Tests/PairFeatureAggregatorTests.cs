using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using FeatureStorage.Memory;
using FluentAssertions;
using Xunit;

namespace FeatureStorage.Tests;

public class PairFeatureAggregatorTests
{
    private class DummyFeatureCodec : IFeatureCodec
    {
        public bool TryEncode(ReadOnlySpan<float> features, Span<byte> dest, out int written)
        {
            for (var i = 0; i < features.Length; i++)
            {
                BinaryPrimitives.WriteSingleLittleEndian(dest[(i * sizeof(float))..], features[i]);
            }

            written = features.Length * sizeof(float);
            return true;
        }

        public bool TryDecode(ReadOnlySpan<byte> src, Span<float> features, out int read)
        {
            for (var i = 0; i < features.Length; i++)
            {
                features[i] = BinaryPrimitives.ReadSingleLittleEndian(src[(i * sizeof(float))..]);
            }

            read = src.Length;
            return true;
        }
    }

    [Fact]
    public void Add_ShouldConnectDataByKey()
    {
        const int featureCount = 64;
        const int sampleDataCount = 16;

        using var aggregator = new PairFeatureAggregator<string, long, DummyFeatureCodec>(10, featureCount, 100, new());

        var features = new float[sampleDataCount][];
        var ids = new long[sampleDataCount];
        var keys = new string[sampleDataCount];

        for (var i = 0; sampleDataCount > i; i++)
        {
            keys[i] = Random.Shared.Next(1, 4).ToString();
            ids[i] = Random.Shared.Next(1000, 2000000);
            features[i] = new float[featureCount];

            for (var j = 0; j < featureCount; j++)
                features[i][j] = Random.Shared.NextSingle();
        }

        for (var i = 0; sampleDataCount > i; i++)
        {
            aggregator.TryAdd(keys[i], ids[i], features[i]).Should().BeTrue();    
        }

        using var container = aggregator.BuildContainer(new SimpleCodec(), new SimpleIndex());

        var uniqueKeys = new Dictionary<string, List<int>>();

        for (var i = 0; keys.Length > i; i++)
        {
            if (!uniqueKeys.TryGetValue(keys[i], out var indices))
            {
                indices = new List<int>();
                uniqueKeys.Add(keys[i], indices);
            }
            
            indices.Add(i);
        }

        // assert

        var recycleMem = new RecycleRegionAllocator(4 * 1024);
        
        foreach (var (key, indices) in uniqueKeys)
        {
            container.TryGet(key, recycleMem, out var block).Should().BeTrue();

            // check ids span
            var blockIds = block.GetIds();
            blockIds.Length.Should().Be(indices.Count);
            
            for (var i = 0; indices.Count > i; i++)
                blockIds[i].Should().Be(ids[indices[i]]);

            // check matrix
            var matrix = block.GetFeatureMatrix();

            for (var i = 0; indices.Count > i; i++)
            {
                var featureSlice = matrix.Slice(i * featureCount, featureCount);
                featureSlice.SequenceEqual(features[indices[i]]);
            }
            
            block.Release();
        }
    }
}