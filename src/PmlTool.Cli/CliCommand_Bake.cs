using System.Text.RegularExpressions;
using NiceIO;
using OkTools.ProcMonUtils;

static partial class Program
{
    const string k_bakeExtraHelp = $@"
# pmltool bake

Make a PML file portable by baking native and Mono symbols into <PML>.pmlbaked.

The .pmlbaked file will be created in the same folder as the PML file. It will have all of the call stack frames from
the PML resolved to their symbolic names. This not only makes the PML file portable, but avoids the need for PDB's or a
Mono 'pmip' file to be present in order to have symbolic call stacks later. Baking is necessary for the `convert` and
`query` commands.

Symbolication happens two ways:

  1. Native frames: `dbghelp.dll` and {NtSymbolPath.EnvVarName} are used to resolve native frames, same as Procmon does. (*)

  2. Unity Mono jit frames: when Unity is run with with env var `UNITY_MIXED_CALLSTACK=1` (**) a `pmip_xxxxx_y.txt` file
     will be created at `%LOCALAPPDATA%\Temp` where `xxxxx` is the Unity process ID and `y` is the domain iteration for
     that session (domain reloads throw away jit'd code). This file contains all the jit'd method names and their
     address ranges.

     For Mono callstacks to be resolved, the matching pmip file needs to be in the same folder as the PML. Note that
     when the Unity process exits, the OS will delete the pmip file, so you need to copy it out of the Temp folder
     before that.

There is a script `<OkTools>/build/PmlTool.Cli/Scripts/UnityCapture.ps1` that will run Unity and Procmon, capture the
pmip file, and bake the PML.

(*) Note that you can use `pmltool resolve` to prefetch PDB's for all modules found in a PML file. This can be useful
when iterating quickly and you don't want to be slowed down by repeatedly failing symbol server queries of private
PDB's. Run the `resolve` on the first PML to ensure you've got all the relevant PDB's, and then for all the `bake` runs
use `--no-symbol-download` to eliminate symbol server queries.

(**) When using `okunity run` to run Unity, it always sets `UNITY_MIXED_CALLSTACK=1` automatically.

";

    static CliExitCode Bake(PmlToolCliArguments opts)
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

        var ntSymbolPath = new NtSymbolPath();
        if (opts.OptNoNtsymbolpath)
            ntSymbolPath.Value = "";
        else if (opts.OptNoSymbolDownload)
        {
            ntSymbolPath = NtSymbolPath.FromEnvironment;

            var oldvar = ntSymbolPath.Value;
            ntSymbolPath.StripDownloadPaths();

            if (ntSymbolPath.Value != oldvar)
                Console.WriteLine($"Replacing {NtSymbolPath.EnvVarName}: {oldvar} -> {ntSymbolPath.Value}");
        }
        else if (NtSymbolPath.FromEnvironment.HasDownloadPaths)
            Console.WriteLine($"{NtSymbolPath.EnvVarName} appears to be set to use a symbol server, which may slow down processing greatly..");

        foreach (var path in opts.OptAddSymbolPath)
            ntSymbolPath.AddPath(path);

        using var pmlReader = new PmlReader(opts.ArgPml!.ToNPath());
        var bakedFile = pmlReader.PmlPath.ChangeExtension(".pmlbaked");

        var iter = 0;
        var symOpts = new SymbolicateOptions
        {
            DebugFormat = opts.OptDebug,
            NtSymbolPath = ntSymbolPath,
            ModuleLoadProgress = name => currentModule = name,
            Progress = (_, total) =>
            {
                if (iter++ == 0)
                    Console.Write($"Writing {total} events to {bakedFile.MakeAbsolute()}...");
                else if (iter % 10000 == 0) Console.Write(".");
            }
        };

        PmlUtils.Symbolicate(pmlReader, symOpts);

        cancel = true;
        Console.WriteLine("done!");

        return CliExitCode.Success;
    }
}
