namespace OkTools.Unity;

public abstract class AssetLmdb : LmdbDatabase
{
    protected AssetLmdb(NPath dbPath, uint supportedVersion) : base(dbPath)
    {
        using var dbVersionTable = new LmdbTable(this, "DBVersion");
        using var tx = Env.BeginReadOnlyTransaction();

        DbVersion = dbVersionTable.GetUint(tx, "Version");

        if (DbVersion != supportedVersion)
            throw new InvalidOperationException($"Unsupported {Name} version {DbVersion} (supported: {supportedVersion})");
    }

    public uint DbVersion { get; }

    public AssetLmdbInfo GetInfo() => new(
            Name,
            $"0x{DbVersion:X}",
            SelectTableNames().OrderBy(s => s.ToLower()).ToArray());
}

public record AssetLmdbInfo(string Name, string Version, string[] TableNames);
