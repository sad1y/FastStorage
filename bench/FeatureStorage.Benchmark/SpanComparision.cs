using System;
using BenchmarkDotNet.Attributes;

namespace FeatureStorage.Benchmark
{
    [DisassemblyDiagnoser(printSource: true)]
    [RyuJitX64Job]
    public class SpanComparision
    {
        private readonly byte[] _a = new byte[16];
        private readonly byte[] _b = new byte[16];

        public SpanComparision()
        {
            var rng = new Random();
            var n = _a.Length;
            while (n > 1)
            {
                var k = rng.Next(n);
                n--;
                (_a[n], _a[k]) = (_a[k], _a[n]);
                k = rng.Next(n);
                (_b[n], _b[k]) = (_b[k], _b[n]);
            }
        }
        
        [Benchmark]
        public bool SequenceEqual()
        {
            var span = _a.AsSpan();
            return span.SequenceEqual(_b);
        }
        
        [Benchmark]
        public bool ForEach()
        {
            var span = _a.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                if (_b[i] != _a[i])
                    return false;
            }

            return true;
        }
    }
}