using System.Text;
using BenchmarkDotNet.Attributes;

namespace KhoiProjectManagement.Benchmarks;

// Example: comparing ways to build a string from many small pieces.
// [Params] runs every benchmark once per value, so the results show how
// each approach scales as input size grows. See README.md for details.
[MemoryDiagnoser]
public class StringBenchmarks
{
    [Params(10, 1000)]
    public int Count { get; set; }

    private string[] _values = [];

    [GlobalSetup]
    public void Setup()
    {
        _values = Enumerable.Range(0, Count).Select(i => i.ToString()).ToArray();
    }

    [Benchmark(Baseline = true)]
    public string StringConcat()
    {
        var result = string.Empty;
        foreach (var value in _values)
        {
            result += value;
        }

        return result;
    }

    [Benchmark]
    public string StringBuilderAppend()
    {
        var builder = new StringBuilder();
        foreach (var value in _values)
        {
            builder.Append(value);
        }

        return builder.ToString();
    }

    [Benchmark]
    public string StringJoin() => string.Join(string.Empty, _values);
}
