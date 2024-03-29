﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OkTools.ProcMonUtils;

public class MonoJitSymbol : IAddressRange
{
    public AddressRange Address;

    // mono pmip files sometimes have the symbol portion blank, but we'll keep an entry anyway to track that there
    // is indeed a jit related function there.
    public string? AssemblyName;
    public string? Symbol;

    ref readonly AddressRange IAddressRange.AddressRef => ref Address;

    public override string ToString()
    {
        var text = AssemblyName ?? "";
        if (Symbol != null)
            text += '!' + Symbol;
        return text;
    }

    public string ToString(ulong addr)
    {
        if (!Address.Contains(addr))
            throw new ArgumentOutOfRangeException(nameof(addr), addr, "Address is not in this symbol");

        var text = "";
        if (!string.IsNullOrEmpty(AssemblyName))
            text += $"[{AssemblyName}] ";
        if (!string.IsNullOrEmpty(Symbol))
            text += $"{Symbol} ";

        text += $"+ 0x{addr-Address.Base:x} (0x{addr:x})";
        return text;
    }
}

[DebuggerDisplay("{System.IO.Path.GetFileName(PmipPath),nq}; {DomainCreationTime.ToString(\"HH:mm:ss.fffffff\"),nq} ({_symbols.Length} symbols)")]
public class MonoJitSymbolDb
{
    readonly MonoJitSymbol[] _symbols; // keep sorted for bsearch

    public IReadOnlyList<MonoJitSymbol> Symbols => _symbols;
    public string PmipPath { get; }

    public MonoJitSymbolDb(string monoPmipPath, DateTime? domainCreationTimeUtc = null)
    {
        // default to creation time of the pmip as a way to detect domain creation

        PmipPath = monoPmipPath;
        DomainCreationTimeUtc = domainCreationTimeUtc ?? File.GetCreationTimeUtc(monoPmipPath);

        // open pmip file - have to do this with compatible flags to how unity is holding it open or it will fail with access violation
        var lines = new List<string>();
        using (var stream = OpenPmipFile(monoPmipPath))
        using (var reader = new StreamReader(stream))
        {
            while (reader.ReadLine() is { } line)
                lines.Add(line);
        }

        // parse pmip

        if (lines[0] != "UnityMixedCallstacks:1.0") // 2.0 coming in https://github.com/Unity-Technologies/mono/pull/1635 (merged jun 14)
            throw new FileLoadException("Mono pmip file has unexpected header or version", monoPmipPath);

        var rx = new Regex(
            @"(?<start>[0-9A-F]{16});"+         // start of range always present
            @"(?<end>[0-9A-F]{16});"+           // end of range (and ;) always present
            @"(\[(?<module>([^\]]+))\]\s+)?"+   // module may not be there if it's a builtin like rgctx_fetch_trampoline_rgctx_2
            @"(?<symbol>.+)?");                 // very rarely, module and symbol both missing

        var entries = new List<MonoJitSymbol>();

        for (var iline = 1; iline != lines.Count; ++iline)
        {
            var lmatch = rx.Match(lines[iline]);
            if (!lmatch.Success)
                throw new FileLoadException($"Mono pmip file has unexpected format line {iline}", monoPmipPath);

            var addressBase = ulong.Parse(lmatch.Groups["start"].Value, NumberStyles.HexNumber);
            var addressSize = (int)(ulong.Parse(lmatch.Groups["end"].Value, NumberStyles.HexNumber) - addressBase);

            var monoJitSymbol = new MonoJitSymbol
            {
                Address = new AddressRange(addressBase, addressSize),
                AssemblyName = lmatch.Groups["module"].Value,
                Symbol = NormalizeMonoSymbolName(lmatch.Groups["symbol"].Value) // remove mono-isms
            };

            // MISSING: handling of a blank assembly+symbol, which *probably* means it's a trampoline

            entries.Add(monoJitSymbol);
        }

        _symbols = entries.OrderBy(e => e.Address.Base).ToArray();
    }

    public DateTime DomainCreationTimeUtc { get; }

    public bool TryFindSymbol(ulong address, [NotNullWhen(returnValue: true)] out MonoJitSymbol? monoJitSymbol) =>
        _symbols.TryFindAddressIn(address, out monoJitSymbol);

    public static string NormalizeMonoSymbolName(string monoSymbol)
    {
        Span<char> chars = stackalloc char[monoSymbol.Length * 2];
        var csb = new CharSpanBuilder(chars);

        for (var i = 0; i < monoSymbol.Length; )
        {
            var c = monoSymbol[i++];
            switch (c)
            {
                case '/':
                    csb.Append('.');
                    break;

                case ':':
                    csb.Append('.');
                    if (monoSymbol[i] == '.')
                        ++i;
                    break;

                case ',' when monoSymbol[i] != ' ':
                    csb.Append(", ");
                    break;

                case ' ' when monoSymbol[i] == '(':
                    csb.Append("(");
                    ++i;
                    break;

                default:
                    csb.Append(c);
                    break;
            }
        }

        return csb.ToString();
    }

    public static (int unityPid, int domainSerial) ParsePmipFilename(string monoPmipPath)
    {
        var match = Regex.Match(Path.GetFileName(monoPmipPath), @"^pmip_(?<pid>\d+)_(?<domain>\d+)\.txt$", RegexOptions.IgnoreCase);

        if (match.Success &&
            int.TryParse(match.Groups["pid"].Value, out var pid) &&
            int.TryParse(match.Groups["domain"].Value, out var domain))
        {
            return (pid, domain);
        }

        throw new FileLoadException("Unable to extract unity PID from mono pmip filename", monoPmipPath);
    }

    // pmip files under active use need to be opened with compatible flags to how unity is holding it open,
    // or it will fail with access violation
    public static FileStream OpenPmipFile(string monoPmipPath) =>
        new(monoPmipPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
}
