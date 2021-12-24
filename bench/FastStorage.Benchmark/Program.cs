using BenchmarkDotNet.Running;

namespace FastStorage.Benchmark
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<SearchTreeVsDictionary>();
        }
    }
}