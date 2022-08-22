using System;
using FluentAssertions;
using Xunit;

namespace FeatureStorage.Tests;

public class PairFeatureAggregatorTests
{
    [Fact]
    public void Add_ShouldConnectDataByKey()
    {
        const int featureSize = 64;
        const int tagSize = 4;
        var aggregator = new PairFeatureAggregator<string>(10, tagSize, featureSize);

        var features = new byte[7][];
        var tags = new byte[7][];
        var rng = new Random();

        for (var i = 0; features.Length > i; i++)
        {
            tags[i] = new byte[tagSize];
            features[i] = new byte[featureSize];
            rng.NextBytes(features[i]);
            rng.NextBytes(tags[i]);
        }

        aggregator.Add("window", tags[0], features[0]);
        aggregator.Add("summer", tags[1], features[1]);
        aggregator.Add("window", tags[2], features[2]);
        aggregator.Add("street", tags[3], features[3]);
        aggregator.Add("window", tags[4], features[4]);
        aggregator.Add("street", tags[5], features[5]);
        aggregator.Add("window", tags[6], features[6]);

        void CheckData(ref PairFeatureAggregator<string>.Iterator iterator, int[] expectedIndices)
        {
            var processed = 0;
            while (iterator.MoveNext())
            {
                var index = expectedIndices[processed];
                var tag = tags[index];
                var feature = features[index];

                var entry = iterator.GetCurrent();
                entry.Tag.ToArray().Should().BeEquivalentTo(tag);
                entry.Features.ToArray().Should().BeEquivalentTo(feature);
                processed++;
            }

            expectedIndices.Length.Should().Be(processed);
        }

        aggregator.Iterate((string key, int count, ref PairFeatureAggregator<string>.Iterator iterator) =>
        {
            switch (key)
            {
                case "window":
                {
                    count.Should().Be(4);
                    CheckData(ref iterator, new[] { 0, 2, 4, 6 });
                    break;
                }
                case "summer":
                {
                    count.Should().Be(1);
                    CheckData(ref iterator, new[] { 1 });
                    break;
                }
                case "street":
                {
                    count.Should().Be(2);
                    CheckData(ref iterator, new[] { 3, 5 });
                    break;
                }
            }
        });
    }
}