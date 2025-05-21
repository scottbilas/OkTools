[PublicAPI]
public static class TaskExtensions
{
    public static async Task<(T1, T2)> WhenAll<T1, T2>(this (Task<T1>, Task<T2>) @this)
    {
        await Task.WhenAll(@this.Item1, @this.Item2);
        return (@this.Item1.Result, @this.Item2.Result);
    }

    public static async Task<(T1, T2, T3)> WhenAll<T1, T2, T3>(this (Task<T1>, Task<T2>, Task<T3>) @this)
    {
        await Task.WhenAll(@this.Item1, @this.Item2, @this.Item3);
        return (@this.Item1.Result, @this.Item2.Result, @this.Item3.Result);
    }

    public static async Task<(T1, T2, T3, T4)> WhenAll<T1, T2, T3, T4>(this (Task<T1>, Task<T2>, Task<T3>, Task<T4>) @this)
    {
        await Task.WhenAll(@this.Item1, @this.Item2, @this.Item3, @this.Item4);
        return (@this.Item1.Result, @this.Item2.Result, @this.Item3.Result, @this.Item4.Result);
    }

    public static async Task<(T1, T2, T3, T4, T5)> WhenAll<T1, T2, T3, T4, T5>(this (Task<T1>, Task<T2>, Task<T3>, Task<T4>, Task<T5>) @this)
    {
        await Task.WhenAll(@this.Item1, @this.Item2, @this.Item3, @this.Item4, @this.Item5);
        return (@this.Item1.Result, @this.Item2.Result, @this.Item3.Result, @this.Item4.Result, @this.Item5.Result
            );
    }
}
