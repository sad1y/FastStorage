using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using FeatureStorage.Containers;

namespace FeatureStorage.Benchmark
{
    [MemoryDiagnoser]
    public class InsertTreeVsDictionary
    {
        private readonly uint[] _values = new uint[50_000_000];

        public InsertTreeVsDictionary()
        {
            for (var i = 0; i < _values.Length; i++)
            {
                _values[i] = (uint)i;
            }
            
            var rng = new Random();
            var n = _values.Length;
            while (n > 1)
            {
                var k = rng.Next(n);
                n--;
                (_values[n], _values[k]) = (_values[k], _values[n]);
            }
        }
        
        [Benchmark(Baseline = true)]
        public object Dictionary()
        {
            var dict = new Dictionary<uint, uint>(_values.Length);

            for (var i = 0; i < _values.Length; i++)
            {
                dict.Add((uint)i, _values[i]);
            }

            return dict;
        }

        [Benchmark]
        public object BPTree()
        {
            using var tree = new BPlusTree(nodeCapacity: 64, (uint)_values.Length);
            
            for (var i = 0; i < _values.Length; i++)
            {
                tree.Insert((uint)i, _values[i]);
            }

            return _values.Length;
        }
        
        [Benchmark]
        public object BPTree_128()
        {
            using var tree = new BPlusTree(nodeCapacity: 128, (uint)_values.Length);
            
            for (var i = 0; i < _values.Length; i++)
            {
                tree.Insert((uint)i, _values[i]);
            }

            return _values.Length;
        }
    }
}