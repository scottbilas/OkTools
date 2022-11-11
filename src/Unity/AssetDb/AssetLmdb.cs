using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using Spreads.Buffers;

namespace OkTools.Unity.AssetDb;

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

    public void DumpTable(DumpContext dump, AssetLmdb db, TableDumpSpec spec)
    {
        using var table = new LmdbTable(this, spec.TableName);
        using var tx = db.Env.BeginReadOnlyTransaction();

        var rows = table.Table.AsEnumerable(tx);
        if (dump.Config.OptLimit != 0)
            rows = rows.Take(dump.Config.OptLimit);

        if (dump.Csv != null)
        {
            Debug.Assert(dump.Json == null);

            dump.Csv.Write($"{spec.CsvFields}\n");

            foreach (var kvp in rows)
            {
                spec.Dump(dump, kvp.Key, kvp.Value);
                dump.Csv.Write('\n');
            }
        }
        else
        {
            Debug.Assert(dump.Json != null);

            if (dump.Config.OptCombined)
            {
                if (spec.UniqueKeys)
                    dump.Json.WriteStartObject(spec.TableName);
                else
                    dump.Json.WriteStartArray(spec.TableName);
            }
            else
            {
                if (spec.UniqueKeys)
                    dump.Json.WriteStartObject();
                else
                    dump.Json.WriteStartArray();
            }


            foreach (var kvp in rows)
            {
                if (!spec.UniqueKeys)
                    dump.Json.WriteStartObject();
                spec.Dump(dump, kvp.Key, kvp.Value);
                if (!spec.UniqueKeys)
                    dump.Json.WriteEndObject();
                dump.NextRow();
            }

            if (spec.UniqueKeys)
                dump.Json.WriteEndObject();
            else
                dump.Json.WriteEndArray();
        }
    }
}

[UsedImplicitly]
public record AssetLmdbInfo(string Name, string Version, string[] TableNames);

public record TableDumpSpec(string TableName, string CsvFields, bool UniqueKeys, Action<DumpContext, DirectBuffer, DirectBuffer> Dump);

public struct DumpConfig
{
    public bool OptJson;
    public bool OptCompact;
    public bool OptCombined;
    public bool OptTrim;
    public int  OptLimit;
}

public sealed class DumpContext : IDisposable
{
    readonly FileStream _file;

    public readonly StreamWriter? Csv;
    public readonly Utf8JsonWriter? Json;
    public readonly DumpConfig Config;

    public DumpContext(string pathNoExtension, DumpConfig config)
    {
        Config = config;

        var path = pathNoExtension + (config.OptJson ? ".json" : ".csv");
        _file = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);

        if (config.OptJson)
        {
            var jsonOptions = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }; // don't want '<' and '>' escaped
            if (!config.OptCompact)
                jsonOptions.Indented = true;
            Json = new Utf8JsonWriter(_file, jsonOptions);
        }
        else
            Csv = new StreamWriter(_file);
    }

    public void Dispose()
    {
        Csv?.Dispose();
        Json?.Dispose();

        _file.Dispose();
    }

    static readonly byte[] k_newline = { (byte)'\n' };

    public void NextRow()
    {
        if (!Config.OptCompact)
            return;

        Csv?.Flush();
        Json?.Flush();

        _file.Write(k_newline);
    }
}
