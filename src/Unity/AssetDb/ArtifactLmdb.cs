using System.Text;
using Spreads.Buffers;
using Spreads.LMDB;
using UnityEngine;

// ReSharper disable SuggestBaseTypeForParameterInConstructor

namespace OkTools.Unity.AssetDb;

public class ArtifactLmdb : AssetLmdb
{
    const uint k_expectedDbVersion = 0x5CE21767;

    public ArtifactLmdb(NPath projectRoot)
        : base(projectRoot.Combine(UnityProjectConstants.ArtifactDbNPath), k_expectedDbVersion) {}
}

public class ArtifactIdPropertyIdToPropertyTable : LmdbTable
{
    public ArtifactIdPropertyIdToPropertyTable(ArtifactLmdb db) : base(db, "ArtifactIDPropertyIDToProperty") {}

    public IEnumerable<(ArtifactId, PropertyDefinition, DirectBuffer)> SelectAll(ReadOnlyTransaction tx)
    {
        foreach (var kvp in Table.AsEnumerable(tx))
        {
            var key = kvp.Key;

            var found = PropertyDefinition.Find(ref key);
            if (found == null)
                throw new InvalidDataException($"Unknown property: {Encoding.ASCII.GetString(key.Span)}");

            var id = key.ReadExpectEnd<ArtifactId>();
            yield return (id, found, kvp.Value);
        }
    }
}

// TODO: ArtifactIDToArtifactDependencies

// TODO: ArtifactIDToArtifactMetaInfo (OMG)
// ArtifactID -> ArtifactMetaInfoBlob

public class ArtifactIdToImportStatsTable : LmdbTable
{
    public ArtifactIdToImportStatsTable(ArtifactLmdb db) : base(db, "ArtifactIDToImportStats") {}

    public IEnumerable<(ArtifactId, ArtifactImportStats)> SelectAll(ReadOnlyTransaction tx) =>
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

