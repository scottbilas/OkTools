#define BENCHMARK

using System;
using System.Linq;
using BenchmarkDotNet.Attributes;

#pragma warning disable CA1822 // benchmarkdotnet does not want statics

[MemoryDiagnoser(displayGenColumns: false)]
[DisassemblyDiagnoser]
[HideColumns("Error", "StdDev", "RatioSD")]
public class CompareModuloVsBranch
{
    static readonly Random s_rng = new(123);

    const int k_arrayLength = 100;
    static readonly int[] k_data = Enumerable
        .Range(0, 50)
        .Select(_ => s_rng.Next(0, k_arrayLength))
        .ToArray();

    [Benchmark(Baseline=true)]
    public void Baseline()
    {
        var list = new TestListAccess(k_arrayLength);

        for (var i = 0; i < k_arrayLength; ++i)
            foreach (var _ in k_data)
                list.Array[i] = 0;
    }

    [Benchmark]
    public void Modulo()
    {
        var list = new TestListAccess(k_arrayLength);

        for (var i = 0; i < k_arrayLength; ++i)
        {
            foreach (var head in k_data)
            {
                var index = (i+head) % list.Array.Length;
                list.Array[index] = 0;
            }
        }
    }

    [Benchmark]
    public void If()
    {
        var list = new TestListAccess(k_arrayLength);

        for (var i = 0; i < k_arrayLength; ++i)
        {
            foreach (var head in k_data)
            {
                var index = i+head;
                if (index >= list.Array.Length)
                {
                    index -= list.Array.Length;
                }
                list.Array[index] = 0;
            }
        }
    }
}

class TestListAccess
{
    public int[] Array;
    //public int Head = 0;
    //public int Used = 0;

    public TestListAccess(int arrayLength)
    {
        Array = new int[arrayLength];
    }
}
