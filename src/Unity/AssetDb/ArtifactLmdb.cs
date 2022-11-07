using Spreads.Buffers;
using Spreads.LMDB;
using UnityEngine;

namespace OkTools.Unity.AssetDb;

public class ArtifactLmdb : AssetLmdb
{
    const uint k_expectedDbVersion = 0x5CE21767;

    public ArtifactLmdb(NPath projectRoot)
        : base(projectRoot.Combine(UnityProjectConstants.ArtifactDbNPath), k_expectedDbVersion) {}
}

// TODO: ArtifactIDPropertyIDToProperty
// TODO: ArtifactIDToArtifactDependencies
// TODO: ArtifactIDToArtifactMetaInfo

public class ArtifactIdToImportStatsTable : LmdbTable
{
    public ArtifactIdToImportStatsTable(ArtifactLmdb db) : base(db, "ArtifactIDToImportStats") {}

    public unsafe IEnumerable<(ArtifactId, ArtifactImportStats)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ReadExpectEnd<ArtifactId>(),
            ReadArtifactImportStats(kvp.Value)));

    static unsafe ArtifactImportStats ReadArtifactImportStats(DirectBuffer value)
    {
        var blob = value.Cast<ArtifactImportStatsBlob>();
        return ArtifactImportStats.Create(blob);
    }
}

public class ArtifactKeyToArtifactIdsTable : LmdbTable
{
    public ArtifactKeyToArtifactIdsTable(ArtifactLmdb db) : base(db, "ArtifactKeyToArtifactIDs") {}

    public IEnumerable<(Hash128, ArtifactIds)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ReadExpectEnd<Hash128>(),
            ReadArtifactIds(kvp.Value)));

    static unsafe ArtifactIds ReadArtifactIds(DirectBuffer value)
    {
        var blob = value.Cast<ArtifactIdsBlob>();
        return new ArtifactIds
        {
            ArtifactKey = blob->ArtifactKey,
            Ids = blob->Ids.ToArrayFromBlob()
        };
    }
}

public class CurrentRevisionsTable : LmdbTable
{
    public CurrentRevisionsTable(ArtifactLmdb db) : base(db, "CurrentRevisions") {}

    public IEnumerable<(Hash128, CurrentRevision)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ReadExpectEnd<Hash128>(),
            kvp.Value.ReadExpectEnd<CurrentRevision>()));
}

public struct CurrentRevision
{
    public BlobArtifactKey ArtifactKey;
    public ArtifactId ArtifactId;
}

public struct BlobArtifactKey
{
    public UnityGuid Guid;
    public BlobImporterId ImporterId;

    public const string CsvHeader = "UnityGuid," + BlobImporterId.CsvHeader;
    public string ToCsv() => $"{Guid},{ImporterId.ToCsv()}";
}

public struct BlobImporterId
{
    public Int32 NativeImporterType;
    public Hash128 ScriptedImporterType;

    public const string CsvHeader = "NativeImporterType,ScriptedImporterType";
    public string ToCsv() => $"{NativeImporterType},{ScriptedImporterType}";
}

public struct ArtifactId
{
    public Hash128 Hash;
}

struct ArtifactIdsBlob
{
    public BlobArtifactKey ArtifactKey;
    public BlobArray<ArtifactId> Ids;
};

public struct ArtifactIds
{
    public BlobArtifactKey ArtifactKey;
    public ArtifactId[] Ids;
}

struct ArtifactImportStatsBlob
{
    // Editor
    public UInt64           ImportTimeMicroseconds;
    public BlobString       ArtifactPath;
    public Int64            ImportedTimestamp;
    public BlobString       EditorRevision;
    public BlobString       UserName;

    // Cache Server
    public UInt16           ReliabilityIndex;
    public Int64            UploadedTimestamp;
    public BlobString       UploadIpAddress;
};

public struct ArtifactImportStats
{
    // Editor
    public UInt64           ImportTimeMicroseconds;
    public string           ArtifactPath;
    public Int64            ImportedTimestamp;
    public string           EditorRevision;
    public string           UserName;

    // Cache Server
    public UInt16           ReliabilityIndex;
    public Int64            UploadedTimestamp;
    public string           UploadIpAddress;

    public const string CsvHeader = "ImportTimeMicroseconds,ArtifactPath,ImportedTimestamp,EditorRevision,UserName,ReliabilityIndex,UploadedTimestamp,UploadIpAddress";
    public string ToCsv() => $"{ImportTimeMicroseconds},{ArtifactPath},{ImportedTimestamp},{EditorRevision},{UserName},{ReliabilityIndex},{UploadedTimestamp},{UploadIpAddress}";

    internal static unsafe ArtifactImportStats Create(ArtifactImportStatsBlob* blob) => new()
    {
        ImportTimeMicroseconds = blob->ImportTimeMicroseconds,
        ArtifactPath = blob->ArtifactPath.GetStringFromBlob(),
        ImportedTimestamp = blob->ImportedTimestamp,
        EditorRevision = blob->EditorRevision.GetStringFromBlob(),
        UserName = blob->UserName.GetStringFromBlob(),
        ReliabilityIndex = blob->ReliabilityIndex,
        UploadedTimestamp = blob->UploadedTimestamp,
        UploadIpAddress = blob->UploadIpAddress.GetStringFromBlob(),
    };
};
