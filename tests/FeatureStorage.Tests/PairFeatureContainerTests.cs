using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Unicode;
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

    private class SimpleIndex : IIndex<string>
    {
        private Dictionary<string, long> _index = new();

        public bool TryGetValue(string key, out long ptr) => _index.TryGetValue(key, out ptr);

        public void Update(string key, long ptr)
        {
            _index[key] = ptr;
        }

        public int Count => _index.Count;

        private const string FileName = "index";
        
        public void Serialize(IDirectory directory)
        {
            var file = directory.CreateFile(FileName);
            using var stream = file.OpenWrite();

            JsonSerializer.Serialize(stream, _index);
            
            stream.Flush();
        }

        public void Deserialize(IDirectory directory)
        {
            var file = directory.GetFile(FileName);
            using var stream = file.OpenRead();
            
            _index = JsonSerializer.Deserialize<Dictionary<string, long>>(stream)!;
        }
    }

    private class SimpleCodec : IPairFeatureCodec<long>
    {
        public unsafe bool TryEncode(ref PairFeatureBlock<long> pairFeatureBlock, Span<byte> dest, out int written)
        {
            var offset = 0;
            fixed (void* destPtr = dest)
            {
                var ids = new Span<long>(destPtr, pairFeatureBlock.Count);
                pairFeatureBlock.GetIds().CopyTo(ids);
                offset += ids.Length * sizeof(long);

                var features = new Span<float>((byte*)destPtr + offset, pairFeatureBlock.GetFeatureMatrix().Length);
                pairFeatureBlock.GetFeatureMatrix().CopyTo(features);
                offset += features.Length * sizeof(float);
            }

            written = offset;
            return true;
        }

        private static readonly RecycleRegionAllocator Allocator = new(4096);

        public bool TryDecode(ReadOnlySpan<byte> src, ref PairFeatureBlock<long> pairFeatureBlock, out int read)
        {
            var size = pairFeatureBlock.Count;
            var structSize = size * sizeof(long) + sizeof(float) * pairFeatureBlock.FeatureCount * size;
            unsafe
            {
                fixed (void* ptr = src)
                {
                    var ids = new Span<long>(ptr, size);
                    pairFeatureBlock = new PairFeatureBlock<long>(Allocator, size, pairFeatureBlock.FeatureCount);
                    ids.CopyTo(pairFeatureBlock.GetIds());

                    var features = new Span<float>((new IntPtr(ptr) + sizeof(long) * size).ToPointer(),
                        size * pairFeatureBlock.FeatureCount);
                    features.CopyTo(pairFeatureBlock.GetFeatureMatrix());
                }
            }

            read = structSize;

            return true;
        }

        public int Stamp => 123;

        public byte Version => 1;
        public int MetaSize => 32;
    }
}