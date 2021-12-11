using System;
using System.Collections.Generic;
using BenchmarkDotNet;
using BenchmarkDotNet.Attributes;

namespace FastStorage.Benchmark
{
    [MemoryDiagnoser]
    public class SearchTreeVsDictionary
    {
        private readonly Dictionary<uint, uint> _dict;
        private readonly BPlusTree _tree;
        private readonly uint[] _values;

        public SearchTreeVsDictionary()
        {
            _values = new uint[10_000_000];
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
            
            _tree = new BPlusTree(nodeCapacity: 64, (uint)_values.Length);
            _dict = new Dictionary<uint, uint>(_values.Length);

            for (var i = 0; i < _values.Length; i++)
            {
                _dict.Add((uint)i, _values[i]);
                _tree.Insert((uint)i, _values[i]);
            }
        }

        [Benchmark(Baseline = true)]
        public uint Dictionary()
        {
            uint cnt = 0;
            for (var i = 0; i < _values.Length; i++)
            {
                _dict.TryGetValue(_values[i], out var r);
                unchecked
                {
                    cnt += r;
                }
            }
            
            return cnt;
        }

        [Benchmark]
        public uint BPTree()
        {
            uint cnt = 0;
            for (var i = 0; i < _values.Length; i++)
            {
                var r= _tree.Search(_values[i]);
                unchecked
                {
                    cnt += r.Value;
                }
            }
            
            return cnt;
        }
    }
}