namespace OkTools.Unity;

// TODO: general note for any lmdb-using code: all of the stuff i'm doing is just to get something working quick. but
// spreads.lmdb gives us direct access to the memory returned by lmdb. there's no need to copy into arrays and strings,
// spreads.lmdb to return structs pointing at the raw memory and defer any processing (like conversion to string) until
// the caller actually wants it.

public abstract class AssetLmdb : LmdbDatabase
{
    protected AssetLmdb(NPath dbPath, uint supportedVersion) : base(dbPath)
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
}

public record AssetLmdbInfo(string Name, string Version, string[] TableNames);
