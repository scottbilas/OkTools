[PublicAPI]
static class ObjectDumperExtensions
{
    public static T DumpConsole<T>(this T @this, int? maxLevel = null)
    {
        // TODO: this doesn't dump base class properties, wat..

        var dumpOptions = new DumpOptions { DumpStyle = DumpStyle.Console };
        if (maxLevel.HasValue)
            dumpOptions.MaxLevel = maxLevel.Value;

        #pragma warning disable RS0030
        Console.WriteLine(ObjectDumper.Dump(@this, dumpOptions));
        #pragma warning restore RS0030

        return @this;
    }
}
