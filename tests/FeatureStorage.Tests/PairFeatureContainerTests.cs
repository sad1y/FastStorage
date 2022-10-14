using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FeatureStorage.Memory;
using FluentAssertions;
using Xunit;

namespace FeatureStorage.Tests;

public class PairFeatureContainerTests
{
    [Fact]
    public void TryGetShouldReturnBlockIfExists()
    {
        var storage = new PairFeatureContainer<SimpleCodec, SimpleIndex, long, long>(new SimpleCodec(), new SimpleIndex(), 5);
        storage.AddOrUpdate(30, 3, (ref PairFeatureBlockBuilder<long> builder, object state) =>
        {
            builder.AddFeatures(10, new float[] { 1, 2, 3, 4, 5 });
            builder.AddFeatures(20, new float[] { 2, 4, 6, 8, 10 });
            builder.AddFeatures(30, new float[] { 3, 6, 9, 12, 15 });
        }, null);

        storage.TryGet(30, out var block).Should().BeTrue();
        block.Count.Should().Be(3);
        block.GetIds().ToArray().Should().BeEquivalentTo(new long[] { 10, 20, 30 });
        block.GetFeatureMatrix().ToArray().Should().BeEquivalentTo(new float[] { 1, 2, 3, 4, 5, 2, 4, 6, 8, 10, 3, 6, 9, 12, 15 });
    }
    
    [Fact]
    public void TryGetShouldNotReturnBlockIfItDoesNotExists()
    {
        var storage = new PairFeatureContainer<SimpleCodec, SimpleIndex, long, long>(new SimpleCodec(), new SimpleIndex(), 5);
        storage.TryGet(30, out _).Should().BeFalse();
    }
    
    [Fact]
    public void SerializeDeserializeShouldReturnSameData()
    {
        var storage = new PairFeatureContainer<SimpleCodec, SimpleIndex, long, long>(new SimpleCodec(), new SimpleIndex(), 5);
        storage.AddOrUpdate(30, 3, (ref PairFeatureBlockBuilder<long> builder, object state) =>
        {
            builder.AddFeatures(10, new float[] { 1, 2, 3, 4, 5 });
            builder.AddFeatures(20, new float[] { 2, 4, 6, 8, 10 });
            builder.AddFeatures(30, new float[] { 3, 6, 9, 12, 15 });
        }, null);

        using var mem = new MemoryStream();
        storage.Serialize(mem);
        mem.Position = 0;
        
        var recoveredStorage = new PairFeatureContainer<SimpleCodec, SimpleIndex, long, long>(new SimpleCodec(), new SimpleIndex(), 5);
        recoveredStorage.Deserialize(mem);
        
        recoveredStorage.TryGet(30, out var block).Should().BeTrue();
        block.Count.Should().Be(3);
        block.GetIds().ToArray().Should().BeEquivalentTo(new long[] { 10, 20, 30 });
        block.GetFeatureMatrix().ToArray().Should().BeEquivalentTo(new float[] { 1, 2, 3, 4, 5, 2, 4, 6, 8, 10, 3, 6, 9, 12, 15 });
    }

    private class SimpleIndex : IIndex<long>
    {
        private readonly Dictionary<long, long> _index = new();
        public IEnumerator<KeyValuePair<long, long>> GetEnumerator() => _index.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool TryGetValue(long key, out long ptr) => _index.TryGetValue(key, out ptr);

        public void Update(long key, long ptr)
        {
            _index[key] = ptr;
        }

        public int Count => _index.Count;
    }

    private class SimpleCodec : IPairFeatureCodec<long>
    {
        public unsafe bool TryEncode(ref PairFeatureBlock<long> pairFeatureBlock, Span<byte> dest, out int written)
        {
            var offset = 0;
            fixed (void* destPtr = dest)
            fixed (void* keysPtr = pairFeatureBlock.GetIds())
            fixed (void* featurePtr = pairFeatureBlock.GetFeatureMatrix())
            {
                var idSize = pairFeatureBlock.Count * sizeof(long);
                Buffer.MemoryCopy(keysPtr, (new IntPtr(destPtr) + offset).ToPointer(), dest.Length, idSize);
                offset += idSize;

                var featureSize = pairFeatureBlock.GetFeatureMatrix().Length * sizeof(float);
                Buffer.MemoryCopy(featurePtr, (new IntPtr(destPtr) + offset).ToPointer(), dest.Length, featureSize);
                offset += featureSize;
            }

            written = offset;
            return true;
        }

        private static readonly RecycleRegionAllocator Allocator = new(4096);

        public bool TryDecode(ReadOnlySpan<byte> src, ref PairFeatureBlock<long> pairFeatureBlock, out int read)
        {
            const int offset = sizeof(int);
            var size = pairFeatureBlock.Count;
            var structSize = size * sizeof(long) + sizeof(float) * pairFeatureBlock.FeatureCount * size;
            unsafe
            {
                fixed (void* ptr = src[offset..])
                {
                    var ids = new Span<long>(ptr, size);
                    pairFeatureBlock = new PairFeatureBlock<long>(Allocator, size, pairFeatureBlock.FeatureCount);
                    ids.CopyTo(pairFeatureBlock.GetIds());
                    
                    var features = new Span<float>((new IntPtr(ptr) + sizeof(long) * size).ToPointer() , size * pairFeatureBlock.FeatureCount);
                    features.CopyTo(pairFeatureBlock.GetFeatureMatrix());
                }
            }

            read = offset + structSize;

            return true;
        }

        public int Stamp => 123;

        public byte Version => 1;
        public int MetaSize => 32;
    }
}