using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Spreads.Buffers;
using Spreads.LMDB;

namespace OkTools.Unity.AssetDb;

// TODO: general note for any lmdb-using code: all of the stuff i'm doing is just to get something working quick. but
// spreads.lmdb gives us direct access to the memory returned by lmdb. there's no need to copy into arrays and strings,
// spreads.lmdb to return structs pointing at the raw memory and defer any processing (like conversion to string) until
// the caller actually wants it.

public class AssetLmdb : LmdbDatabase
{
    public AssetLmdb(NPath dbPath, uint supportedVersion) : base(dbPath)
    {
        using var dbVersionTable = new LmdbTable(this, "DBVersion");
        using var tx = Env.BeginReadOnlyTransaction();

        DbVersion = dbVersionTable.GetUint(tx, "Version");

        if (DbVersion != supportedVersion)
            throw new InvalidOperationException($"Unsupported {Name} version {DbVersion:X} (supported: {supportedVersion:X})");
    }

    public uint DbVersion { get; }

    public AssetLmdbInfo GetInfo() => new(
            Name,
            $"0x{DbVersion:X}",
            SelectTableNames().OrderBy(s => s.ToLower()).ToArray());

    public void DumpTable(TableDumpSpec spec, string pathNoExtension, bool useCsv)
    {
        using var table = new LmdbTable(this, spec.TableName);
        using var dump = new DumpContext(pathNoExtension, this, useCsv);

        if (dump.Csv != null)
        {
            Debug.Assert(dump.Json == null);

            dump.Csv.Write($"{spec.CsvFields}\n");

            foreach (var kvp in table.Table.AsEnumerable(dump.Tx))
            {
                spec.Dump(dump, kvp.Key, kvp.Value);
                dump.Csv.Write('\n');
            }
        }
        else
        {
            Debug.Assert(dump.Json != null);

            if (spec.UniqueKeys)
                dump.Json.WriteStartObject();
            else
                dump.Json.WriteStartArray();

            foreach (var kvp in table.Table.AsEnumerable(dump.Tx))
            {
                if (!spec.UniqueKeys)
                    dump.Json.WriteStartObject();
                spec.Dump(dump, kvp.Key, kvp.Value);
                if (!spec.UniqueKeys)
                    dump.Json.WriteEndObject();
                dump.Newline();
            }

            if (spec.UniqueKeys)
                dump.Json.WriteEndObject();
            else
                dump.Json.WriteEndArray();
        }
    }
}

public record AssetLmdbInfo(string Name, string Version, string[] TableNames);
public record TableDumpSpec(string TableName, string CsvFields, bool UniqueKeys, Action<DumpContext, DirectBuffer, DirectBuffer> Dump);

public sealed class DumpContext : IDisposable
{
    readonly FileStream _file;

    public readonly StreamWriter? Csv;
    public readonly Utf8JsonWriter? Json;
    public readonly StringBuilder Buffer = new();
    public readonly ReadOnlyTransaction Tx;

    public DumpContext(string pathNoExtension, LmdbDatabase db, bool useCsv)
    {
        var path = pathNoExtension + (useCsv ? ".csv" : ".json");
        _file = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);

        if (useCsv)
            Csv = new StreamWriter(_file);
        else
            Json = new Utf8JsonWriter(_file, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }); // don't want '<' and '>' escaped

        Tx = db.Env.BeginReadOnlyTransaction();
    }

    public void Dispose()
    {
        Tx.Dispose();
        Csv?.Dispose();
        Json?.Dispose();
        _file.Dispose();
    }

    static readonly byte[] k_newline = { (byte)'\n' };

    public void Newline()
    {
        Csv?.Flush();
        Json?.Flush();

        _file.Write(k_newline);
    }
}
