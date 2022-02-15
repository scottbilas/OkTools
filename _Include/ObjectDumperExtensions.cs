[PublicAPI]
static class ObjectDumperExtensions
{
    public static T DumpConsole<T>(this T @this, int? maxLevel = null)
    {
        var dumpOptions = new DumpOptions { DumpStyle = DumpStyle.Console };
        if (maxLevel.HasValue)
            dumpOptions.MaxLevel = maxLevel.Value;

        Console.WriteLine(ObjectDumper.Dump(@this, dumpOptions));

        return @this;
    }
}
