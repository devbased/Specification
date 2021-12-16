using CommandLine;

namespace Benchmark.Specification;

[Verb("run", HelpText = "Run the benchmark.")]
internal class BenchmarkOptions
{
    [Value(0, Required = true, HelpText = "The benchmark name to run.")]
    public string BenchmarkName { get; set; }
}

[Verb("info", HelpText = "Information about existing benchmarks.")]
internal class Information
{
}
