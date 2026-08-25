using BenchmarkDotNet.Attributes;

namespace KhoiProjectManagement.Benchmarks;

// Example: comparing lookup strategies over a collection of a given size.
// See README.md for how to read [Params]/[MemoryDiagnoser] output.
[MemoryDiagnoser]
public class CollectionBenchmarks
{
    [Params(100, 10_000)]
    public int Count { get; set; }

    private List<int> _list = [];
    private HashSet<int> _hashSet = [];
    private Dictionary<int, int> _dictionary = [];
    private int _searchValue;

    [GlobalSetup]
    public void Setup()
    {
        _list = Enumerable.Range(0, Count).ToList();
        _hashSet = [.. _list];
        _dictionary = _list.ToDictionary(i => i, i => i);
        _searchValue = Count - 1; // worst case for a linear scan
    }

    [Benchmark(Baseline = true)]
    public bool ListContains() => _list.Contains(_searchValue);

    [Benchmark]
    public bool HashSetContains() => _hashSet.Contains(_searchValue);

    [Benchmark]
    public bool DictionaryContainsKey() => _dictionary.ContainsKey(_searchValue);

    [Benchmark]
    public List<int> LinqWhereToList() => _list.Where(i => i % 2 == 0).ToList();

    [Benchmark]
    public List<int> ForLoopFilter()
    {
        var result = new List<int>(_list.Count / 2);
        foreach (var value in _list)
        {
            if (value % 2 == 0)
            {
                result.Add(value);
            }
        }

        return result;
    }
}
