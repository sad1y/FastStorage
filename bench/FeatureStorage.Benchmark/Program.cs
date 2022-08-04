using BenchmarkDotNet.Running;

namespace FeatureStorage.Benchmark
{
    public class Program
    {
        static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}