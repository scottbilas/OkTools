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
// TODO: ArtifactIDToImportStats
// TODO: ArtifactKeyToArtifactIDs

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
}

public struct BlobImporterId
{
    public Int32 NativeImporterType;
    public Hash128 ScriptedImporterType;
}

public struct ArtifactId
{
    public Hash128 Hash;
}
