using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using FeatureStorage.Containers;

namespace FeatureStorage.Benchmark;

[MemoryDiagnoser]
public class SearchTreeVsDictionary
{
    private readonly Dictionary<uint, uint> _dict;
    private readonly BPlusTree _tree;
    private readonly uint[] _values;
    private readonly BPlusTree _tree128;

    public SearchTreeVsDictionary()
    {
        _values = new uint[50_000_000];
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

        Console.WriteLine($"tree_64: {_tree}");
            
        _tree128 = new BPlusTree(nodeCapacity: 128, (uint)_values.Length);
        Console.WriteLine($"tree_128: {_tree128}");
            
        _dict = new Dictionary<uint, uint>(_values.Length);

        for (var i = 0; i < _values.Length; i++)
        {
            _dict.Add((uint)i, _values[i]);
            _tree.Insert((uint)i, _values[i]);
            _tree128.Insert((uint)i, _values[i]);
        }

        Console.WriteLine("after insert");
        Console.WriteLine($"tree_64: {_tree}");
        Console.WriteLine($"tree_128: {_tree128}");
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
    public uint BPTree_64()
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
        
        
    [Benchmark]
    public uint BPTree_128()
    {
        uint cnt = 0;
        for (var i = 0; i < _values.Length; i++)
        {
            var r= _tree128.Search(_values[i]);
            unchecked
            {
                cnt += r.Value;
            }
        }
            
        return cnt;
    }
}