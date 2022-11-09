using System.Text;
using Spreads.Buffers;
using UnityEngine;

namespace OkTools.Unity.AssetDb;

public static class ArtifactLmdb
{
    const uint k_expectedDbVersion = 0x5CE21767;

    public static AssetLmdb OpenLmdb(NPath projectRoot) =>
        new(projectRoot.Combine(UnityProjectConstants.ArtifactDbNPath), k_expectedDbVersion);

    public static readonly TableDumpSpec[] All = AssetLmdbTableAttribute.CreateTableDumpSpecs(typeof(ArtifactLmdb));

    [AssetLmdbTable("ArtifactIDPropertyIDToProperty", "ArtifactID,Property,IsInMetaFile,Value0,Value1,...", UniqueKeys = true)]
    public static void DumpArtifactIdPropertyIdToProperty(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        var (property, id) = PropertyDefinition.Get<ArtifactId>(key);

        dump.Json?.WriteStartObject(id.Hash.ToString());

        if (dump.Csv != null)
            dump.Csv.Write($"{id.Hash},{property.Name},{property.IsInMetaFile},");
        else
        {
            dump.Json!.WriteString("Property", property.Name);
            dump.Json.WriteBoolean("IsInMetaFile", property.IsInMetaFile);
        }

        LmdbValue.Dump(dump, property.ValueType, ref value);
        value.ExpectEnd();

        dump.Json?.WriteEndObject();
    }

// TODO: ArtifactIDToArtifactDependencies
#if NO
    [AssetLmdbTable("", "")]
    public static void Dump(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
    }
#endif

// TODO: ArtifactIDToArtifactMetaInfo (OMG)
// ArtifactID -> ArtifactMetaInfoBlob
#if NO
    [AssetLmdbTable("", "")]
    public static void Dump(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
    }
#endif


    [AssetLmdbTable("ArtifactIDToImportStats", "ArtifactID,ImportTimeMicroseconds,ArtifactPath,ImportedTimestamp,EditorRevision,UserName,ReliabilityIndex,UploadedTimestamp,UploadIpAddress", UniqueKeys = true)]
    public static unsafe void DumpArtifactIdToImportStats(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        var id = key.ReadExpectEnd<ArtifactId>();
        var stats = value.Cast<ArtifactImportStatsBlob>();

        if (dump.Csv != null)
        {
            dump.Csv.Write(
                $"{id.Hash},"+
                $"{stats->ImportTimeMicroseconds},{stats->ArtifactPath},{stats->ImportedTimestampAsDateTime},{stats->EditorRevision},"+
                $"{stats->UserName},{stats->ReliabilityIndex},{(stats->UploadedTimestamp != 0 ? stats->UploadedTimestampAsDateTime.ToString() : "0")},{stats->UploadIpAddress}");
        }
        else
        {
            dump.Json!.WriteStartObject(id.Hash.ToString());
                dump.Json.WriteString("ArtifactPath", stats->ArtifactPath.ToString());
                dump.Json.WriteString("ImportedTimestamp", stats->ImportedTimestampAsDateTime);
                dump.Json.WriteString("EditorRevision", stats->EditorRevision.ToString());
                dump.Json.WriteString("UserName", stats->UserName.ToString());
                dump.Json.WriteNumber("ReliabilityIndex", stats->ReliabilityIndex);
                if (stats->UploadedTimestamp != 0)
                    dump.Json.WriteString("UploadedTimestamp", stats->UploadedTimestampAsDateTime);
                else
                    dump.Json.WriteNumber("UploadedTimestamp", 0);
                dump.Json.WriteString("UploadIpAddress", stats->UploadIpAddress.ToString());
            dump.Json.WriteEndObject();
        }
    }

    static void Write(DumpContext dump, in ArtifactKey key)
    {
        if (dump.Csv != null)
            dump.Csv.Write($"{key.Guid},");
        else
            dump.Json!.WriteString("UnityGuid", key.Guid.ToString());

        Write(dump, key.ImporterId);
    }

    static void Write(DumpContext dump, in ImporterId importerId)
    {
        if (dump.Csv != null)
            dump.Csv.Write($"{importerId.NativeImporterType},{importerId.ScriptedImporterType}");
        else
        {
            dump.Json!.WriteStartObject("ImporterId");
                dump.Json.WriteNumber("NativeImporterType", importerId.NativeImporterType);
                dump.Json.WriteString("ScriptedImporterType", importerId.ScriptedImporterType.ToString());
            dump.Json.WriteEndObject();
        }
    }

    [AssetLmdbTable("ArtifactKeyToArtifactIDs", "ArtifactKeyHash,UnityGuid,NativeImporterType,ScriptedImporterType,ArtifactId0,ArtifactId1,...", UniqueKeys = true)]
    public static unsafe void Dump(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        var keyHash = key.ReadExpectEnd<Hash128>();
        var valueBlob = value.Cast<ArtifactIdsBlob>();

        dump.Csv?.Write($"{keyHash},");
        dump.Json?.WriteStartObject(keyHash.ToString());

        Write(dump, valueBlob->ArtifactKey);

        if (dump.Csv != null)
        {
            for (var i = 0; i < valueBlob->Ids.Length; ++i)
                dump.Csv.Write($",{valueBlob->Ids.RefElementFromBlob(i)->Hash}");
        }
        else
        {
            dump.Json!.WriteStartArray("Ids");
            for (var i = 0; i < valueBlob->Ids.Length; ++i)
                dump.Json.WriteStringValue(valueBlob->Ids.RefElementFromBlob(i)->Hash.ToString());
            dump.Json.WriteEndArray();
        }

        dump.Json?.WriteEndObject();
    }

    [AssetLmdbTable("CurrentRevisions", "ArtifactKeyHash,UnityGuid,NativeImporterType,ScriptedImporterType,ArtifactId", UniqueKeys = true)]
    public static void DumpCurrentRevisions(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        var keyHash = key.ReadExpectEnd<Hash128>();
        var rev = value.ReadExpectEnd<CurrentRevision>();

        if (dump.Csv != null)
        {
            dump.Csv.Write(
                $"{keyHash},{rev.ArtifactKey.Guid},{rev.ArtifactKey.ImporterId.NativeImporterType},"+
                $"{rev.ArtifactKey.ImporterId.ScriptedImporterType},{rev.ArtifactId.Hash}");
        }
        else
        {
            dump.Json!.WriteStartObject(keyHash.ToString());
                Write(dump, rev.ArtifactKey);
                dump.Json.WriteString("ArtifactId", rev.ArtifactId.Hash.ToString());
            dump.Json.WriteEndObject();
        }
    }

}
