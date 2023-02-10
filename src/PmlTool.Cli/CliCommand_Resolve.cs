using OkTools.ProcMonUtils;

static partial class Program
{
    const string k_resolveExtraHelp = @"
# pmltool resolve

Try to resolve symbols for every module found in PML.

Your _NT_SYMBOL_PATH should be set to download symbols, so that this command will preload PDB's for the PML into your
local symbol store. See the note in `pmltool help bake` for why this is useful.

Example _NT_SYMBOL_PATH:

   srv*C:\Symbols*https://msdl.microsoft.com/download/symbols

Note that the symbol server is fairly slow to download and many of the PDB's caught up in a broad capture session
can be very large.
";

    static CliExitCode Resolve(PmlToolCliArguments opts)
    {
        if (!NtSymbolPath.FromEnvironment.HasDownloadPaths)
            Console.WriteLine($"{NtSymbolPath.EnvVarName} appears to be not set to use a symbol server!");

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

        var dbghelp = new DbgHelpInstance();
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
