using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OkTools.ProcMonUtils;

[DebuggerDisplay("{AssemblyName}!{Symbol}")]
public class MonoJitSymbol : IAddressRange
{
    public AddressRange Address;

    // mono pmip files sometimes have the symbol portion blank, but we'll keep an entry anyway to track that there
    // is indeed a jit related function there.
    public string? AssemblyName;
    public string? Symbol;

    ref readonly AddressRange IAddressRange.AddressRef => ref Address;
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

        // parse pmip

        var lines = File.ReadAllLines(monoPmipPath);
        if (lines[0] != "UnityMixedCallstacks:1.0") // 2.0 coming in https://github.com/Unity-Technologies/mono/pull/1635 (merged jun 14)
            throw new FileLoadException("Mono pmip file has unexpected header or version", monoPmipPath);

        var rx = new Regex(
            @"(?<start>[0-9A-F]{16});"+         // start of range always present
            @"(?<end>[0-9A-F]{16});"+           // end of range (and ;) always present
            @"(\[(?<module>([^\]]+))\]\s+)?"+   // module may not be there if it's a builtin like rgctx_fetch_trampoline_rgctx_2
            @"(?<symbol>.+)?");                 // very rarely, module and symbol both missing

        var entries = new List<MonoJitSymbol>();

        for (var iline = 1; iline != lines.Length; ++iline)
        {
            var lmatch = rx.Match(lines[iline]);
            if (!lmatch.Success)
                throw new FileLoadException($"Mono pmip file has unexpected format line {iline}", monoPmipPath);

            var addressBase = long.Parse(lmatch.Groups["start"].Value, NumberStyles.HexNumber);
            var addressSize = (int)(long.Parse(lmatch.Groups["end"].Value, NumberStyles.HexNumber) - addressBase);

            var monoJitSymbol = new MonoJitSymbol
            {
                Address = new AddressRange(addressBase, addressSize),
                AssemblyName = lmatch.Groups["module"].Value,
                Symbol = lmatch.Groups["symbol"].Value
                    .Replace(" (", "(").Replace('/', '.').Replace(':', '.').Replace(",", ", "), // remove mono-isms
            };

            // MISSING: handling of a blank assembly+symbol, which *probably* means it's a trampoline

            entries.Add(monoJitSymbol);
        }

        _symbols = entries.OrderBy(e => e.Address.Base).ToArray();
    }

    public DateTime DomainCreationTimeUtc { get; }

    public bool TryFindSymbol(long address, [NotNullWhen(returnValue: true)] out MonoJitSymbol? monoJitSymbol) =>
        _symbols.TryFindAddressIn(address, out monoJitSymbol);

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
}
