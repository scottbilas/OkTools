using System.Text.RegularExpressions;
using NiceIO;
using OkTools.ProcMonUtils;

static partial class Program
{
    const string k_ntSymbolPathName = "_NT_SYMBOL_PATH";

    static CliExitCode Bake(PmlToolCliArguments cliOptions)
    {
        // FUTURE: this eats up a ton of native memory, probably from loading and using all the PDB's and never unloading them.
        // can probably set up some kind of LRU+pdbsize unload strategy to keep it manageable.

        var cancel = false;
        string? currentModule = null;

        // yuck, replace this with nice RX
        var monitor = new Thread(() =>
        {
            DateTime? lastModuleUpdateTime = DateTime.Now;
            string? lastModule = null;

            // ReSharper disable once AccessToModifiedClosure LoopVariableIsNeverChangedInsideLoop
            while (!cancel)
            {
                var localCurrentModule = currentModule;
                if (localCurrentModule != lastModule)
                {
                    lastModuleUpdateTime = DateTime.Now;
                    lastModule = localCurrentModule;
                }
                else if (localCurrentModule != null && lastModuleUpdateTime != null)
                {
                    var now = DateTime.Now;
                    if ((now - lastModuleUpdateTime.Value).TotalSeconds > 2)
                    {
                        lastModuleUpdateTime = null;
                        Console.Write($"[loading {localCurrentModule}]");
                    }
                }

                Thread.Sleep(50);
            }
        });
        monitor.Start();

        string? ntSymbolPath = null;
        if (cliOptions.OptNoNtsymbolpath)
        {
            ntSymbolPath = "";
        }
        else if (cliOptions.OptNoSymbolDownload)
        {
            var oldvar = Environment.GetEnvironmentVariable(k_ntSymbolPathName);
            if (oldvar != null)
            {
                var newvar = Regex.Replace(oldvar, @"\bSRV\*([^*]+)\*http[^;]+", "$1", RegexOptions.IgnoreCase);
                if (newvar != oldvar)
                {
                    Console.WriteLine($"Replacing {k_ntSymbolPathName}: {oldvar} -> {newvar}");
                    ntSymbolPath = newvar;
                }
            }
        }
        else if ((Environment.GetEnvironmentVariable(k_ntSymbolPathName)?.IndexOf("http") ?? -1) != -1)
            Console.WriteLine($"{k_ntSymbolPathName} appears to be set to use a symbol server, which may slow down processing greatly..");

        using var pmlReader = new PmlReader(cliOptions.ArgPml!.ToNPath());
        var bakedFile = pmlReader.PmlPath.ChangeExtension(".pmlbaked");

        var iter = 0;
        PmlUtils.Symbolicate(pmlReader, new SymbolicateOptions {
            DebugFormat = cliOptions.OptDebug,
            NtSymbolPath = ntSymbolPath,
            ModuleLoadProgress = name => currentModule = name,
            Progress = (_, total) =>
            {
                if (iter++ == 0)
                    Console.Write($"Writing {total} events to {bakedFile.MakeAbsolute()}...");
                else if (iter % 10000 == 0) Console.Write(".");
            }});

        cancel = true;
        Console.WriteLine("done!");

        return CliExitCode.Success;
    }
}
