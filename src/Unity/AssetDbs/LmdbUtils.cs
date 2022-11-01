using System.Reflection;
using System.Text;
using Spreads.Buffers;
using Spreads.LMDB;

namespace OkTools.Unity;

public static class LmdbUtils
{
    public static DirectBuffer StringToBuffer(Dictionary<string, DirectBuffer> cache, string str)
    {
        if (cache.TryGetValue(str, out var buffer))
            return buffer;

        var bytes = new byte[str.Length + 1]; // need null terminator
        Encoding.ASCII.GetBytes(str.AsSpan(), bytes.AsSpan());

        cache.Add(str, buffer = new(bytes));
        return buffer;
    }

    public static string ToAsciiString(this DirectBuffer @this)
    {
        var span = @this.Span[^1] == 0 ? @this.Span[..^1] : @this.Span; // sometimes it's null terminated, sometimes not
        return Encoding.ASCII.GetString(span);
    }

    public static Database OpenDatabase(this LMDBEnvironment @this, string? name) =>
        @this.OpenDatabase(name, new DatabaseConfig(DbFlags.None));
}

public class LmdbDatabase : IDisposable
{
    readonly LMDBEnvironment? _env;
    readonly Dictionary<string, DirectBuffer> _stringBufferCache = new();

    public LmdbDatabase(NPath dbPath)
    {
        DbPath = dbPath;
        TempPath = NPath.CreateTempDirectory(Assembly.GetExecutingAssembly().GetName().Name!);

        try
        {
            // TODO: Spreads.LMDB does not support NOSUBDIR option. It takes a path and expects that this path is
            // only the directory name, because it tries to auto-create the directory if it doesn't exist. NOSUBDIR
            // says to use that path as the mdb file itself. No way to do that without hacking it, so we make a
            // temp copy instead.

            File.Copy(dbPath, TempPath.Combine("data.mdb"));
            _env = LMDBEnvironment.Create(TempPath);
            _env.Open();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _env?.Dispose();
        TempPath.DeleteIfExists();
    }

    public NPath DbPath { get; }
    public string Name => DbPath.FileNameWithoutExtension;
    public NPath TempPath { get; }
    public LMDBEnvironment Env => _env!;

    public DirectBuffer StringToBuffer(string str) =>
        LmdbUtils.StringToBuffer(_stringBufferCache, str);

    public IEnumerable<string> SelectTableNames()
    {
        using var metaTable = Env.OpenDatabase(null);
        using var tx = Env.BeginReadOnlyTransaction();

        foreach (var item in metaTable.AsEnumerable(tx))
            yield return item.Key.ToAsciiString();
    }
}

public class LmdbTable : IDisposable
{
    readonly LmdbDatabase _db;
    readonly Database _table;

    public LmdbTable(LmdbDatabase db, string tableName)
    {
        _db = db;
        _table = db.Env.OpenDatabase(tableName);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _table.Dispose();
    }

    public Database Table => _table;
    public string Name => _table.Name;

    public ReadOnlySpan<byte> Get(ReadOnlyTransaction tx, string key)
    {
        var matchKey = _db.StringToBuffer(key);
        if (!_table.TryGet(tx, ref matchKey, out var found))
            throw new InvalidOperationException($"Can't find {_db.Name}.{Name}.{key}");

        return found.Span;
    }

    public uint GetUint(ReadOnlyTransaction tx, string key)
    {
        var matchKey = _db.StringToBuffer(key);
        if (!_table.TryGet(tx, ref matchKey, out uint found))
            throw new InvalidOperationException($"Can't find {_db.Name}.{Name}.{key}");

        return found;
    }
}
