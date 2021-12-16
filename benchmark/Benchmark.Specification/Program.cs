using System.Reflection;
using System.Text;
using Benchmark.Specification;
using BenchmarkDotNet.Running;
using CommandLine;

var parserResult = Parser.Default.ParseArguments<BenchmarkOptions, Information>(args);
parserResult
    .WithParsed<BenchmarkOptions>(opt =>
    {
        var benchmarkName = opt.BenchmarkName.Replace("benchmark", string.Empty, StringComparison.OrdinalIgnoreCase) + "Benchmark";
        var benchmarkType = Assembly.GetExecutingAssembly()
            .GetTypes()
            .SingleOrDefault(x => x.Name.Equals(benchmarkName, StringComparison.OrdinalIgnoreCase));
        if (benchmarkType == null)
        {
            throw new ArgumentException($"Benchmark '{benchmarkName}' not found!");
        }
        BenchmarkRunner.Run(benchmarkType);
    })
    .WithParsed<Information>(_ =>
    {
        var benchmarks = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(x => x.Name.EndsWith("Benchmark", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Name.Replace("Benchmark", string.Empty, StringComparison.OrdinalIgnoreCase));

        var info = new StringBuilder();
        info.AppendLine("Benchmarks:");
        foreach (var benchmark in benchmarks)
        {
            info.AppendLine($" - {benchmark}");
        }

        Console.WriteLine(info);
    });