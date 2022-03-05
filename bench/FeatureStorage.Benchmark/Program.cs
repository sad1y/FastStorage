using BenchmarkDotNet.Running;

namespace FeatureStorage.Benchmark
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<SearchTreeVsDictionary>();
        }
    }
}