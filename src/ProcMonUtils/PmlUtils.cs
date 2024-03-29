﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Vanara.Extensions;
using Vanara.PInvoke;

namespace OkTools.ProcMonUtils;

class SymCache : IDisposable
{
    readonly DbgHelpInstance _simpleSymbolHandler;
    readonly HashSet<string> _symbolsForModuleCache = new();
    readonly Dictionary<ulong, (DbgHelp.SYMBOL_INFO symbol, int offset)> _symbolFromAddressCache = new();
    readonly List<MonoJitSymbolDb> _monoJitSymbolDbs = new();

    public SymCache(SymbolicateOptions options) =>
        _simpleSymbolHandler = new DbgHelpInstance(options.NtSymbolPath);

    public void Dispose() => _simpleSymbolHandler.Dispose();

    public void LoadModule(PmlModule module)
    {
        if (!_symbolsForModuleCache.Add(module.ImagePath))
            return;

        var win32Error = _simpleSymbolHandler.LoadModule(module.ImagePath, module.Address.Base);
        if (win32Error != Win32Error.ERROR_SUCCESS &&
            win32Error != Win32Error.ERROR_PATH_NOT_FOUND &&
            win32Error != Win32Error.ERROR_NO_MORE_FILES) // this can happen if a dll has been deleted since the PML was recorded
            throw win32Error.GetException();
    }

    public void LoadMonoSymbols(string pmipPath, DateTime? domainCreationTimeUtc = null)
    {
        _monoJitSymbolDbs.Add(new MonoJitSymbolDb(pmipPath, domainCreationTimeUtc));
        _monoJitSymbolDbs.Sort((a, b) => a.DomainCreationTimeUtc < b.DomainCreationTimeUtc ? 1 : -1); // newer domains first so we can use `>` while iterating forward
    }

    public bool TryGetNativeSymbol(ulong address, out (DbgHelp.SYMBOL_INFO symbol, int offset) symOffset)
    {
        if (_symbolFromAddressCache.TryGetValue(address, out symOffset))
            return true;

        var win32Error = _simpleSymbolHandler.GetSymbolFromAddress(address, out symOffset.symbol, out symOffset.offset);
        switch ((uint)win32Error)
        {
            case Win32Error.ERROR_SUCCESS:
                _symbolFromAddressCache.Add(address, symOffset);
                return true;
            case Win32Error.ERROR_INVALID_ADDRESS: // jetbrains fsnotifier.exe can cause this, wild guess that it happens with in-memory generated code
            case Win32Error.ERROR_MOD_NOT_FOUND: // this can happen if a dll has been deleted since the PML was recorded or if it is privileged (like crowdstrike)
                return false;
            default:
                throw win32Error.GetException();
        }
    }

    // sometimes can get addresses that seem like they're in the mono jit memory space, but don't actually match any symbols. why??
    public bool TryGetMonoSymbol(DateTime eventTimeUtc, ulong address, [NotNullWhen(returnValue: true)] out MonoJitSymbol? monoJitSymbol)
    {
        foreach (var reader in _monoJitSymbolDbs)
        {
            if (eventTimeUtc < reader.DomainCreationTimeUtc)
                continue;

            if (!reader.TryFindSymbol(address, out monoJitSymbol))
                break;

            return true;
        }

        monoJitSymbol = default!;
        return false;
    }
}

public struct SymbolicateOptions
{
    public bool DebugFormat;           // defaults to string dictionary to compact the file and improve parsing speed a bit
    public bool IgnorePmipCreateTimes; // don't use the filesystem's createtime for a pmip file to include it in a valid event range or not
    public string[]? MonoPmipPaths;    // defaults to looking for matching pmip's in pml folder
    public string? BakedPath;          // defaults to <pmlname>.pmlbaked
    public NtSymbolPath NtSymbolPath;  // defaults to null, which will have dbghelp use _NT_SYMBOL_PATH if exists
    public Range? EventRange;          // range of indices to symbolicate (defaults to all)
    public Action<int, int>? Progress;
    public Action<string?>? ModuleLoadProgress;
}

public class SymbolicateException : Exception
{
    public SymbolicateException(string message) : base(message) { }
    public SymbolicateException(string message, Exception exception) : base(message, exception) { }
}

public static class PmlUtils
{
    public const string CaptureTimeFormat = "HH:mm:ss.fffffff";

    // the purpose of this function is to bake text symbols (for native and mono jit) so the data can be transferred
    // to another machine without needing the exact same binaries and pdb's.
    public static void Symbolicate(PmlReader pmlReader, SymbolicateOptions options = default)
    {
        // MISSING: support for domain reloads. pass in a timestamp to use (which would in non-test scenarios just come from
        // a stat for create-time on the pmip file itself) and the symbolicator can use the event create time to figure out
        // which pmip set to use.

        options.Progress?.Invoke(0, pmlReader.EventCount);

        var symCacheDb = new Dictionary<int /*pid*/, SymCache>();
        var bakedBin = new PmlBakedWriter(pmlReader.EventCount);

        var pmipPaths = options.MonoPmipPaths ?? pmlReader.PmlPath.Parent.Files("pmip_*.txt").Select(p => p.ToString()!);
        var pmipFileDb = new Dictionary<int, List<NPath>>();

        foreach (var pmipPath in pmipPaths)
        {
            var (pid, _) = MonoJitSymbolDb.ParsePmipFilename(pmipPath);
            if (!pmipFileDb.TryGetValue(pid, out var pmipFiles))
                pmipFileDb.Add(pid, pmipFiles = new List<NPath>());
            pmipFiles.Add(pmipPath);
        }

        var bakedPath = options.BakedPath ?? pmlReader.PmlPath.ChangeExtension(".pmlbaked");
        var tmpBakedPath = bakedPath + ".tmp";
        var bakedText = options.DebugFormat ? File.CreateText(tmpBakedPath) : null;

        using (bakedText)
        {
            bakedText?.Write("# Frame Types: " +  Enum.GetValues<FrameType>().Select(t => $"{(char)t}={t}").StringJoin(", ") + "\n\n");

            var pmlEvents = pmlReader
                .SelectEvents(PmlReader.Filter.AllEventClasses | PmlReader.Filter.Stacks, options.EventRange)
                .Where(e => e.Frames!.Length != 0);

            foreach (var pmlEvent in pmlEvents)
            {
                var process = pmlReader.ResolveProcess(pmlEvent.ProcessIndex);

                if (!symCacheDb.TryGetValue(process.ProcessId, out var symCache))
                {
                    pmipFileDb.TryGetValue(process.ProcessId, out var pmipFiles);

                    // need to add the folder of the process in order to have dbghelp notice the mono pdb file there.
                    // probably don't want to do this in general because sym paths end up getting checked recursively..
                    var localOptions = options;
                    if (pmipFiles != null)
                        localOptions.NtSymbolPath.AddPath(Path.GetDirectoryName(process.GetImagePath())!);

                    symCacheDb.Add(process.ProcessId, symCache = new SymCache(localOptions));

                    if (pmipFiles != null)
                    {
                        DateTime? createTime = localOptions.IgnorePmipCreateTimes ? default(DateTime) : null;
                        foreach (var pmipFile in pmipFiles)
                        {
                            localOptions.ModuleLoadProgress?.Invoke(pmipFile);
                            symCache.LoadMonoSymbols(pmipFile, createTime);
                            localOptions.ModuleLoadProgress?.Invoke(null);
                        }
                    }
                }

                bakedText?.Write($"Event #{pmlEvent.EventIndex} at {pmlEvent.CaptureDateTime.ToString(CaptureTimeFormat)}\n");

                for (var (iframe, count) = (0, pmlEvent.Frames!.Length); iframe < count; ++iframe)
                {
                    bakedText?.Write($"    {count-iframe-1:00} ");

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
                            throw new SymbolicateException($"Symbol lookup fail for {module.ImagePath} at 0x{address:x}", e);
                        }
                    }

                    var frameType = (address & (1UL << 63)) != 0 ? FrameType.Kernel : FrameType.User;

                    if (module != null && symCache.TryGetNativeSymbol(address, out var nativeSymbol))
                    {
                        var name = nativeSymbol.symbol.Name;

                        // sometimes we get noisy symbols like Microsoft.CodeAnalysis.CommitHashAttribute..ctor(System.String)$##6000AE6
                        var found = name.IndexOf("$##", StringComparison.Ordinal);
                        if (found != -1)
                            name = name[..found];

                        bakedText?.Write($"{frameType.ToChar()} [{module.ModuleName}] {name} + 0x{nativeSymbol.offset:x} (0x{address:x})\n");
                        bakedBin.AddFrame(pmlEvent.EventIndex, frameType, module.ModuleName, name, nativeSymbol.offset);
                    }
                    else if (symCache.TryGetMonoSymbol(pmlEvent.CaptureDateTimeUtc, address, out var monoSymbol) && monoSymbol.AssemblyName != null && monoSymbol.Symbol != null)
                    {
                        var monoOffset = (int)(address - monoSymbol.Address.Base);

                        if (bakedText != null)
                        {
                            // TODO: switch this to ToString(address) - not trivial, it breaks a test that needs
                            // debugging, possible format change due to different testing of null/empty in ToString vs
                            // this here code.
                            bakedText.Write("M");
                            if (monoSymbol.AssemblyName.Length > 0)
                                bakedText.Write($" [{monoSymbol.AssemblyName}]");
                            bakedText.Write($" {monoSymbol.Symbol} + 0x{monoOffset:x} (0x{address:x})\n");
                        }

                        bakedBin.AddFrame(pmlEvent.EventIndex, FrameType.Mono, monoSymbol.AssemblyName, monoSymbol.Symbol, monoOffset);
                    }
                    else
                    {
                        bakedText?.Write($"{frameType.ToChar()} 0x{address:x}\n");
                        bakedBin.AddFrame(pmlEvent.EventIndex, frameType, address);
                    }
                }

                bakedText?.Write('\n');

                options.Progress?.Invoke(pmlEvent.EventIndex, pmlReader.EventCount);
            }

            foreach (var cache in symCacheDb.Values)
                cache.Dispose();

            if (!options.DebugFormat)
            {
                using var file = File.Create(tmpBakedPath);
                bakedBin.Write(file);
            }
        }

        File.Delete(bakedPath);
        File.Move(tmpBakedPath, bakedPath);
    }
}

// AddressOrOffset: address if no symbol, offset if yes symbol
[StructLayout(LayoutKind.Sequential, Pack=1)]
public readonly record struct PmlBakedFrame(FrameType Type, int ModuleStringIndex, int SymbolStringIndex, ulong AddressOrOffset)
{
    public override string ToString() => SymbolStringIndex != 0
        ? $"{Type.ToChar()} [{ModuleStringIndex}] {SymbolStringIndex} + 0x{AddressOrOffset:x}"
        : $"{Type.ToChar()} [{ModuleStringIndex}] 0x{AddressOrOffset:x}";

    public string ToString(PmlBakedReader reader) => SymbolStringIndex != 0
        ? $"{Type.ToChar()} [{reader.GetCharSpan(ModuleStringIndex)}] {reader.GetCharSpan(SymbolStringIndex)} + 0x{AddressOrOffset:x}"
        : $"{Type.ToChar()} [{reader.GetCharSpan(ModuleStringIndex)}] 0x{AddressOrOffset:x}";
}

public class PmlBakedWriter
{
    const int k_version = 3;
    internal static readonly byte[] MagicHeader = $"PMLBAKED:{k_version}->".GetBytes(false, CharSet.Ansi);

    readonly OkList<int> _frameCounts; // by event index
    readonly OkList<PmlBakedFrame> _frames;
    readonly OkList<int> _stringOffsets;
    readonly OkList<char> _strings;
    readonly Dictionary<string, int> _stringDb = new();

    public PmlBakedWriter(int eventReserve)
    {
        _frameCounts = new(eventReserve);
        _frames = new(eventReserve * 20);
        _stringOffsets = new(10000);
        _strings = new(10000);

        _stringOffsets.Add(0);
        _stringDb.Add("", 0);
    }

    public void Write(Stream stream)
    {
        stream.Write(MagicHeader);

        using var compressor = new DeflateStream(stream, CompressionMode.Compress, true);

        var writer = new BinaryWriter(compressor);

        Write(writer, _frameCounts);
        Write(writer, _frames);
        Write(writer, _stringOffsets);
        Write(writer, _strings);
    }

    static void Write<T>(BinaryWriter writer, OkList<T> list) where T : unmanaged
    {
        writer.Write(list.Count);
        writer.Write(MemoryMarshal.AsBytes(list.AsSpan));
    }

    public void AddFrame(int eventIndex, FrameType frameType, string moduleName, string symbolName, int offset)
    {
        IncrementFrame(eventIndex);
        _frames.Add(new PmlBakedFrame(frameType, ToStringIndex(moduleName), ToStringIndex(symbolName), (ulong)offset));
    }

    public void AddFrame(int eventIndex, FrameType frameType, ulong address)
    {
        IncrementFrame(eventIndex);
        _frames.Add(new PmlBakedFrame(frameType, 0, 0, address));
    }

    void IncrementFrame(int eventIndex)
    {
        if (_frameCounts.Count <= eventIndex)
            _frameCounts.Count = eventIndex + 1;
        ++_frameCounts[eventIndex];
    }

    static readonly char[] k_badChars = { '\n', '\r', '\t' };

    int ToStringIndex(string str)
    {
        if (_stringDb.TryGetValue(str, out var index))
            return index;

        if (str.Length == 0)
            throw new ArgumentException("Shouldn't have an empty string here");
        if (str.IndexOfAny(k_badChars) != -1)
            throw new ArgumentException("String has bad chars in it");

        index = _stringOffsets.Count;
        _stringDb.Add(str, index);
        _stringOffsets.Add(_strings.Count);
        _strings.AddRange(str);

        return index;
    }
}

public class PmlBakedReader
{
    readonly OkList<int> _frameOffsets = new(0); // by event index
    readonly OkList<PmlBakedFrame> _frames = new(0);
    readonly OkList<int> _stringOffsets = new(0);
    readonly OkList<char> _strings = new(0);

    public PmlBakedReader(Stream stream)
    {
        var header = new byte[PmlBakedWriter.MagicHeader.Length];
        if (stream.Read(header) != header.Length || !header.SequenceEqual(PmlBakedWriter.MagicHeader))
            throw new PmlBakedParseException("Not a .pmlbaked file, or is corrupt");

        var decompressor = new DeflateStream(stream, CompressionMode.Decompress, true);
        var reader = new BinaryReader(decompressor);

        Read(reader, _frameOffsets, 1);

        // convert counts to offsets
        var offset = 0;
        for (var i = 0; i < _frameOffsets.Count; ++i)
        {
            var next = _frameOffsets[i];
            _frameOffsets[i] = offset;
            offset += next;
        }
        _frameOffsets.Add(offset); // add terminator for simpler lookups

        Read(reader, _frames);
        Read(reader, _stringOffsets, 1);
        Read(reader, _strings);
        _stringOffsets.Add(_strings.Count); // add terminator for simpler lookups
    }

    public int EventCount => _frameOffsets.Count - 1;

    public ReadOnlySpan<PmlBakedFrame> GetFrames(int eventIndex)
    {
        var start = _frameOffsets[eventIndex];
        var end = _frameOffsets[eventIndex + 1];

        return _frames[start..end];
    }

    public ReadOnlySpan<char> GetCharSpan(int stringIndex)
    {
        var start = _stringOffsets[stringIndex];
        var end = _stringOffsets[stringIndex + 1];

        return _strings[start..end];
    }

    static void Read<T>(BinaryReader reader, OkList<T> list, int extraCapacity = 0) where T : unmanaged
    {
        var count = reader.ReadInt32();
        list.Capacity = count + extraCapacity; // this avoids a realloc when we optionally add a terminator or whatever
        list.Count = count;

        var span = MemoryMarshal.AsBytes(list.AsSpan);
        var totalRead = 0;

        while (totalRead < span.Length)
        {
            var read = reader.Read(span[totalRead..]);
            if (read == 0)
                throw new PmlBakedParseException("Unexpected end of file");

            totalRead += read;
        }

        Debug.Assert(totalRead == span.Length);
    }
}
