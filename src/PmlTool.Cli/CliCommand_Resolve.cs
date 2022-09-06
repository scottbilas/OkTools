using NiceIO;
using OkTools.ProcMonUtils;

static partial class Program
{
    static CliExitCode Resolve(PmlToolCliArguments opts)
    {
        if ((Environment.GetEnvironmentVariable(k_ntSymbolPathName)?.IndexOf("http") ?? -1) == -1)
            Console.WriteLine($"{k_ntSymbolPathName} appears to be not set to use a symbol server!");

        Console.Write("Scanning call stacks for modules...");

        var modulePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var iter = 0;
        using (var pmlReader = new PmlReader(opts.ArgPml!.ToNPath()))
        {
            foreach (var pmlEvent in pmlReader.SelectEvents())
            {
                foreach (var address in pmlEvent.Frames!)
                {
                    if (pmlReader.ResolveProcess(pmlEvent.ProcessIndex).TryFindModule(address, out var module))
                        modulePaths.Add(module.ImagePath);

                    if (iter++ % 10000000 == 0)
                        Console.Write(".");
                }
            }
        }
        Console.WriteLine("done!");

        var dbghelp = new SimpleSymbolHandler();
        foreach (var (modulePath, index) in modulePaths.OrderBy(_ => _).Select((p, i) => (p, b: i)))
        {
            var start = DateTime.Now;
            Console.Write($"{index+1}/{modulePaths.Count} Loading symbols for {modulePath}...");
            try
            {
                dbghelp.LoadModule(modulePath);
                var delta = DateTime.Now - start;
                Console.WriteLine($"done! ({delta.TotalSeconds:0.00}s)");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"fail! {e.GetType().Name}: {e.Message}");
            }
        }

        return CliExitCode.Success;
    }
}
