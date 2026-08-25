# KhoiProjectManagement.Benchmarks

Microbenchmarks for backend code, using [BenchmarkDotNet](https://benchmarkdotnet.org/). This project references
`KhoiProjectManagement.Domain`, `KhoiProjectManagement.Application`, and `KhoiProjectManagement.Infrastructure`, so
any entity, service, or repository from those layers can be constructed and measured directly here — not just the
generic string/collection examples below.

## Running benchmarks

BenchmarkDotNet refuses to run a Debug build (results would be meaningless), so always build/run in Release:

```bash
# from the repo root
dotnet run -c Release --project KhoiProjectManagement.Benchmarks
```

Running with no arguments launches an interactive menu listing every `[Benchmark]` class found in the assembly —
pick one (or `*` for all) by number.

To skip the menu, pass a filter that matches the class and/or method name:

```bash
# every benchmark in StringBenchmarks
dotnet run -c Release --project KhoiProjectManagement.Benchmarks -- --filter "*StringBenchmarks*"

# a single method
dotnet run -c Release --project KhoiProjectManagement.Benchmarks -- --filter "*StringBenchmarks.StringJoin"

# everything
dotnet run -c Release --project KhoiProjectManagement.Benchmarks -- --filter "*"
```

Each run writes an HTML/CSV/Markdown report per class under `BenchmarkDotNet.Artifacts/results/` (gitignored —
treat it as scratch output, not something to commit).

## What's included

- **[StringBenchmarks.cs](StringBenchmarks.cs)** — string concatenation (`+=`) vs. `StringBuilder` vs.
  `string.Join`, at two input sizes.
- **[CollectionBenchmarks.cs](CollectionBenchmarks.cs)** — `List<T>.Contains` (linear scan) vs. `HashSet<T>` vs.
  `Dictionary<TKey,TValue>` lookups, and a LINQ `Where().ToList()` vs. a hand-written `for`-loop filter, at two
  input sizes.

Both are meant as copy-from-this templates, not a fixed suite — see below for how to add your own.

## Writing a new benchmark

1. Add a new class anywhere in this project (one file per topic, e.g. `PermissionResolverBenchmarks.cs`).
2. Mark methods you want measured with `[Benchmark]`. BenchmarkDotNet discovers `[Benchmark]` methods on any
   public class automatically — no registration needed, no `Program.cs` changes.
3. Build and run as above; the new class shows up in the menu / matches your `--filter`.

Minimal example:

```csharp
using BenchmarkDotNet.Attributes;

namespace KhoiProjectManagement.Benchmarks;

public class MyBenchmarks
{
    [Benchmark]
    public int DoWork() => Enumerable.Range(0, 100).Sum();
}
```

### Key attributes

- **`[Benchmark]`** — marks a method to be measured. A class can have several; BenchmarkDotNet runs and reports
  each independently.
- **`[Benchmark(Baseline = true)]`** — marks one method per class as the baseline. Every other benchmark's result
  is then also shown as a `Ratio` relative to it (e.g. `0.42x`), which is usually more useful than raw
  nanoseconds when comparing approaches. Exactly one method per class should set this.
- **`[MemoryDiagnoser]`** (class-level attribute) — adds `Allocated`/`Gen0` columns showing heap allocations per
  operation, not just speed. Almost always worth adding — an approach that's faster but allocates far more may
  not be the win it looks like.
- **`[Params(...)]`** on a public property — runs *every* benchmark in the class once per value, so you can see
  how an approach scales with input size (e.g. `[Params(10, 1000)]` on a `Count` property, as in
  `StringBenchmarks`/`CollectionBenchmarks` above). Use `[GlobalSetup]` to (re)build test data from the current
  `Params` value before each size is measured.
- **`[GlobalSetup]`** — a method run once before all iterations of a benchmark (per `Params` combination). Use it
  to build input data so the setup cost isn't included in the measured time. There's a matching `[GlobalCleanup]`
  if you need to dispose anything.
- **`[Arguments(...)]`** / **`[ArgumentsSource(...)]`** — pass specific argument values into a `[Benchmark]`
  method directly, as an alternative to `[Params]` when the values are per-method rather than per-class.

### Benchmarking your own Domain/Application/Infrastructure code

Because this project references those three layers, you can new up and call real project code — construct a
service directly (with fakes/in-memory data for its dependencies, the same way the unit tests under `tests/`
do), or exercise a pure algorithm (e.g. `SpacePermissionResolver`'s tree-walk, a mapping method, a validator)
without spinning up the API or a database. Anything that needs `IRepository<T>`/`IUnitOfWork`/a real
`ProjectManagementContext` is possible but heavier — prefer isolating the specific hot-path logic you want to
measure rather than benchmarking through a full EF Core round-trip unless the query itself is what you're
measuring.

## Reading the results

BenchmarkDotNet prints a Markdown table like:

| Method              | Count | Mean       | Ratio | Allocated |
|---------------------|-------|------------|-------|-----------|
| StringConcat        | 1000  | 45.230 us  | 1.00  | 2 MB      |
| StringBuilderAppend | 1000  | 12.100 us  | 0.27  | 12 KB     |
| StringJoin          | 1000  | 9.800 us   | 0.22  | 8 KB      |

- **Mean** — average time per call (lower is better).
- **Ratio** — Mean divided by the baseline's Mean (lower is better; the baseline is always `1.00`).
- **Allocated** — bytes allocated per call (only shown with `[MemoryDiagnoser]`; lower is better).
- A row per `Params` value/combination — read `Count` (or whatever property you added) alongside the numbers to
  see how each approach scales.

Prefer the `Ratio` column over raw `Mean` when comparing approaches — absolute numbers vary between machines, but
the relative comparison is what tells you which approach actually wins.
