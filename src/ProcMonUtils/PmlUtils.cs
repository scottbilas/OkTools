using System.Diagnostics.CodeAnalysis;
using System.Text;
using BetterWin32Errors;
using Zodiacon.DebugHelp;

namespace OkTools.ProcMonUtils;

class SymCache : IDisposable
{
    readonly SimpleSymbolHandler _simpleSymbolHandler;
    readonly HashSet<string> _symbolsForModuleCache = new();
    readonly Dictionary<ulong, (SymbolInfo symbol, ulong offset)> _symbolFromAddressCache = new();
    MonoSymbolReader? _monoSymbolReader;

    public SymCache(SymbolicateOptions options) =>
        _simpleSymbolHandler = new SimpleSymbolHandler(options.NtSymbolPath);

    public void Dispose() => _simpleSymbolHandler.Dispose();

    public void LoadModule(PmlModule module)
    {
        if (!_symbolsForModuleCache.Add(module.ImagePath))
            return;

        var win32Error = _simpleSymbolHandler.LoadModule(module.ImagePath, module.Address.Base);
        if (win32Error != Win32Error.ERROR_SUCCESS &&
            win32Error != Win32Error.ERROR_PATH_NOT_FOUND &&
            win32Error != Win32Error.ERROR_NO_MORE_FILES) // this can happen if a dll has been deleted since the PML was recorded
            throw new Win32Exception(win32Error);
    }

    public void LoadMonoSymbols(string monoPmipPath)
    {
        if (_monoSymbolReader != null)
            throw new InvalidOperationException("Multiple mono symbol sets not supported yet");

        _monoSymbolReader = new MonoSymbolReader(monoPmipPath);
    }

    public bool TryGetNativeSymbol(ulong address, out (SymbolInfo symbol, ulong offset) symOffset)
    {
        if (_symbolFromAddressCache.TryGetValue(address, out symOffset))
            return true;

        var win32Error = _simpleSymbolHandler.GetSymbolFromAddress(address, ref symOffset.symbol, out symOffset.offset);
        switch (win32Error)
        {
            case Win32Error.ERROR_SUCCESS:
                _symbolFromAddressCache.Add(address, symOffset);
                return true;
            case Win32Error.ERROR_INVALID_ADDRESS: // jetbrains fsnotifier.exe can cause this, wild guess that it happens with in-memory generated code
            case Win32Error.ERROR_MOD_NOT_FOUND: // this can happen if a dll has been deleted since the PML was recorded
                return false;
            default:
                throw new Win32Exception(win32Error);
        }
    }

    // sometimes can get addresses that seem like they're in the mono jit memory space, but don't actually match any symbols. why??
    public bool TryGetMonoSymbol(ulong address, [NotNullWhen(returnValue: true)] out MonoJitSymbol? monoJitSymbol)
    {
        if (_monoSymbolReader != null)
            return _monoSymbolReader.TryFindSymbol(address, out monoJitSymbol);

        monoJitSymbol = default!;
        return false;
    }
}

public struct SymbolicateOptions
{
    public bool DebugFormat;        // defaults to string dictionary to compact the file and improve parsing speed a bit
    public string[]? MonoPmipPaths; // defaults to looking for matching pmip's in pml folder
    public string? BakedPath;       // defaults to <pmlname>.pmlbaked
    public string? NtSymbolPath;    // defaults to null, which will have dbghelp use _NT_SYMBOL_PATH if exists
    public Action<int, int>? Progress;
    public Action<string?>? ModuleLoadProgress;
    public string[]? NoSymbolModuleNames;

    public int StartAtEventIndex;
    public int EventProcessCount; // 0 means "all remaining events"
}

public class SymbolicateException : Exception
{
    public SymbolicateException(string message) : base(message) { }
    public SymbolicateException(string message, Exception exception) : base(message, exception) { }
}

public static class PmlUtils
{
    public const string CaptureTimeFormat = "hh:mm:ss.fffffff tt";

    // the purpose of this function is to bake text symbols (for native and mono jit) so the data can be transferred
    // to another machine without needing the exact same binaries and pdb's.
    public static int Symbolicate(string inPmlPath, SymbolicateOptions options = default)
    {
        // MISSING: support for domain reloads. pass in a timestamp to use (which would in non-test scenarios just come from
        // a stat for create-time on the pmip file itself) and the symbolicator can use the event create time to figure out
        // which pmip set to use.

        var pmlPath = inPmlPath.ToNPath();
        using var pmlReader = new PmlReader(pmlPath);
        options.Progress?.Invoke(0, (int)pmlReader.EventCount);

        var symCacheDb = new Dictionary<uint /*pid*/, SymCache>();
        var strings = new List<string> { "" };
        var stringDb = new Dictionary<string, int>() { { "", 0 } };
        var badChars = new[] { '\n', '\r', '\t' };

        int ToStringIndex(string str)
        {
            if (!stringDb.TryGetValue(str, out var index))
            {
                if (str.Length == 0)
                    throw new ArgumentException("Shouldn't have an empty string here");
                if (str.IndexOfAny(badChars) != -1)
                    throw new ArgumentException("String has bad chars in it");

                stringDb.Add(str, index = strings.Count);
                strings.Add(str);
            }

            return index;
        }

        var monoPmipPaths = options.MonoPmipPaths ?? pmlPath.Parent.Files("pmip*.txt").Select(p => p.ToString()).ToArray();

        foreach (var monoPmipPath in monoPmipPaths)
        {
            var (pid, _) = MonoSymbolReader.ParsePmipFilename(monoPmipPath);
            if (symCacheDb.ContainsKey((uint)pid))
                throw new SymbolicateException("Multiple mono domains not supported yet");

            var symCache = new SymCache(options);
            symCache.LoadMonoSymbols(monoPmipPath);
            symCacheDb.Add((uint)pid, symCache);
        }

        var bakedPath = options.BakedPath ?? pmlPath.ChangeExtension(".pmlbaked");
        var tmpBakedPath = bakedPath + ".tmp";
        using (var bakedFile = File.CreateText(tmpBakedPath))
        {
            if (options.DebugFormat)
            {
                bakedFile.Write("# Events: Sequence:Time of Day;PID;Frame[0];Frame[1];Frame[..n]\n");
                bakedFile.Write("# Frames: $type [$module] $symbol + $offset (type: K=kernel, U=user, M=mono)\n");
                bakedFile.Write("\n");
            }
            bakedFile.Write("[Config]\n");
            bakedFile.Write($"EventCount={pmlReader.EventCount}\n");
            if (options.DebugFormat)
                bakedFile.Write($"DebugFormat={options.DebugFormat}\n");
            bakedFile.Write("\n");
            bakedFile.Write("[Events]\n");

            var sb = new StringBuilder();

            var pmlEvents = pmlReader.SelectEvents(PmlReader.Filter.FileSystem | PmlReader.Filter.Stacks, options.StartAtEventIndex);
            if (options.EventProcessCount > 0)
                pmlEvents = pmlEvents.Take(options.EventProcessCount);

            foreach (var pmlEvent in pmlEvents)
            {
                var process = pmlReader.ResolveProcess(pmlEvent.ProcessIndex);

                if (!symCacheDb.TryGetValue(process.ProcessId, out var symCache))
                    symCacheDb.Add(process.ProcessId, symCache = new SymCache(options));

                if (options.DebugFormat)
                {
                    sb.AppendFormat("{0}:{1};{2};",
                        pmlEvent.EventIndex,
                        pmlEvent.CaptureDateTime.ToString(CaptureTimeFormat),
                        process.ProcessId);
                }
                else
                {
                    sb.AppendFormat("{0:x}:{1:x};{2:x};",
                        pmlEvent.EventIndex,
                        pmlEvent.CaptureTime,
                        process.ProcessId);
                }

                for (var iframe = 0; iframe < pmlEvent.Frames!.Length; ++iframe)
                {
                    var address = pmlEvent.Frames[iframe];

                    if (process.TryFindModule(address, out var module))
                    {
                        try
                        {
                            options.ModuleLoadProgress?.Invoke(module.ModuleName);
                            symCache.LoadModule(module);
                            options.ModuleLoadProgress?.Invoke(null);
                        }
                        catch (Exception e)
                        {
                            throw new SymbolicateException($"Symbol lookup fail for {module.ImagePath} at 0x{address:X}", e);
                        }
                    }

                    var type = (address & 1UL << 63) != 0 ? 'K' : 'U';
                    if (iframe != 0)
                        sb.Append(';');

                    if (module != null && symCache.TryGetNativeSymbol(address, out var nativeSymbol))
                    {
                        var name = nativeSymbol.symbol.Name;

                        // sometimes we get noisy symbols like Microsoft.CodeAnalysis.CommitHashAttribute..ctor(System.String)$##6000AE6
                        var found = name.IndexOf("$##");
                        if (found != -1)
                            name = name[..found];

                        if (options.DebugFormat)
                            sb.AppendFormat("{0} [{1}] {2} + 0x{3:x}", type, module.ModuleName, name, nativeSymbol.offset);
                        else
                            sb.AppendFormat("{0},{1:x},{2:x},{3:x}", type, ToStringIndex(module.ModuleName), ToStringIndex(name), nativeSymbol.offset);
                    }
                    else if (symCache.TryGetMonoSymbol(address, out var monoSymbol) && monoSymbol.AssemblyName != null && monoSymbol.Symbol != null)
                    {
                        if (options.DebugFormat)
                            sb.AppendFormat("M [{0}] {1} + 0x{2:x}", monoSymbol.AssemblyName, monoSymbol.Symbol, address - monoSymbol.Address.Base);
                        else
                            sb.AppendFormat("M,{0:x},{1:x},{2:x}", ToStringIndex(monoSymbol.AssemblyName), ToStringIndex(monoSymbol.Symbol), address - monoSymbol.Address.Base);
                    }
                    else
                        sb.AppendFormat(options.DebugFormat ? "{0} 0x{1:x}" : "{0},{1:x}", type, address);
                }

                sb.Append('\n');
                bakedFile.Write(sb.ToString());
                sb.Clear();

                options.Progress?.Invoke(pmlEvent.EventIndex, (int)pmlReader.EventCount);
            }

            foreach (var cache in symCacheDb.Values)
                cache.Dispose();

            if (strings.Count != 0)
            {
                bakedFile.Write("\n");
                bakedFile.Write("[Strings]\n");

                foreach (var (s, i) in strings.Select((s, i) => (s, i)))
                {
                    bakedFile.Write($"{i:x}:{s}");
                    bakedFile.Write('\n');
                }
            }
        }

        File.Delete(bakedPath);
        File.Move(tmpBakedPath, bakedPath);

        return (int)pmlReader.EventCount;
    }
}
