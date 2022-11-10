using Spreads.Buffers;
using UnityEngine;

namespace OkTools.Unity.AssetDb;

public static class ArtifactLmdb
{
    const uint k_expectedDbVersion = 0x5CE21767;

    public static AssetLmdb OpenLmdb(NPath projectRoot) =>
        new(projectRoot.Combine(UnityProjectConstants.ArtifactDbNPath), k_expectedDbVersion);

    public static readonly TableDumpSpec[] All = AssetLmdbTableAttribute.CreateTableDumpSpecs(typeof(ArtifactLmdb));

    [AssetLmdbTable("ArtifactIDPropertyIDToProperty", "ArtifactID,Property,ValueType,IsInMetaFile,Value0,Value1,...")]
    public static void DumpArtifactIdPropertyIdToProperty(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // ArtifactDB.cpp: ArtifactDB::m_ArtifactIDPropertyIDToProperty

        var (property, id) = PropertyDefinition.Get<ArtifactID>(key);

        if (dump.Csv != null)
            dump.Csv.Write($"{id.value},{property.Name},{property.ValueType},{property.IsInMetaFile},");
        else
        {
            dump.Json!.WriteString("ArtifactID", id.value.ToString());
            dump.Json.WriteString("Property", property.Name);
            dump.Json.WriteString("ValueType", property.ValueType.ToString());
            dump.Json.WriteBoolean("IsInMetaFile", property.IsInMetaFile);
        }

        LmdbValue.Dump(dump, property.ValueType, ref value);
        value.ExpectEnd();
    }

#if NO
    [AssetLmdbTable("ArtifactIDToArtifactDependencies", "")]
    public static void DumpArtifactIdToArtifactDependencies(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // ArtifactDB.cpp: ArtifactDB::m_ArtifactIDToArtifactDependencies

        var id = key.Read<ArtifactID>();
    }
#endif

// TODO: ArtifactIDToArtifactMetaInfo (OMG)
#if NO
        // ArtifactDB.cpp: ArtifactDB::m_ArtifactIDToArtifactMetaInfo
    [AssetLmdbTable("", "")]
    public static void Dump(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
    }
#endif


    [AssetLmdbTable("ArtifactIDToImportStats", "ArtifactID,ImportTimeMicroseconds,ArtifactPath,ImportedTimestamp,EditorRevision,UserName,ReliabilityIndex,UploadedTimestamp,UploadIpAddress", UniqueKeys = true)]
    public static unsafe void DumpArtifactIdToImportStats(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // ArtifactDB.cpp: ArtifactDB::m_ArtifactIDToImportStats

        var id = key.ReadExpectEnd<ArtifactID>();
        var stats = value.Cast<ArtifactImportStats>();

        if (dump.Csv != null)
        {
            dump.Csv.Write(
                $"{id.value},"+
                $"{stats->importTimeMicroseconds},{stats->artifactPath.GetString()},{stats->ImportedTimestampAsDateTime},{stats->editorRevision.GetString()},"+
                $"{stats->userName.GetString()},{stats->reliabilityIndex},"+
                $"{(stats->uploadedTimestamp != 0 ? stats->UploadedTimestampAsDateTime : "0")},{stats->uploadIpAddress.GetString()}");
        }
        else
        {
            dump.Json!.WriteStartObject(id.value.ToString());
                dump.Json.WriteString("ArtifactPath", stats->artifactPath.GetString());
                dump.Json.WriteString("ImportedTimestamp", stats->ImportedTimestampAsDateTime);
                dump.Json.WriteString("EditorRevision", stats->editorRevision.GetString());
                dump.Json.WriteString("UserName", stats->userName.GetString());
                dump.Json.WriteNumber("ReliabilityIndex", stats->reliabilityIndex);
                if (stats->uploadedTimestamp != 0)
                    dump.Json.WriteString("UploadedTimestamp", stats->UploadedTimestampAsDateTime);
                else
                    dump.Json.WriteNumber("UploadedTimestamp", 0);
                dump.Json.WriteString("UploadIpAddress", stats->uploadIpAddress.GetString());
            dump.Json.WriteEndObject();
        }
    }

    [AssetLmdbTable("ArtifactKeyToArtifactIDs", "ArtifactKeyHash,UnityGuid,NativeImporterType,ScriptedImporterType,ArtifactId0,ArtifactId1,...", UniqueKeys = true)]
    public static unsafe void Dump(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // ArtifactDB.cpp: ArtifactDB::m_ArtifactKeyHashToArtifactIDs

        var keyHash = key.ReadExpectEnd<Hash128>();
        var valueBlob = value.Cast<ArtifactIDs>();

        dump.Csv?.Write($"{keyHash},");
        dump.Json?.WriteStartObject(keyHash.ToString());

        Write(dump, valueBlob->artifactKey);

        if (dump.Csv != null)
        {
            for (var i = 0; i < valueBlob->ids.Length; ++i)
                dump.Csv.Write($",{valueBlob->ids.PtrAt(i)->value}");
        }
        else
        {
            dump.Json!.WriteStartArray("ids");
            for (var i = 0; i < valueBlob->ids.Length; ++i)
                dump.Json.WriteStringValue(valueBlob->ids.PtrAt(i)->value.ToString());
            dump.Json.WriteEndArray();
        }

        dump.Json?.WriteEndObject();
    }

    [AssetLmdbTable("CurrentRevisions", "ArtifactKeyHash,UnityGuid,NativeImporterType,ScriptedImporterType,ArtifactId", UniqueKeys = true)]
    public static void DumpCurrentRevisions(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // ArtifactDB.cpp: ArtifactDB::m_CurrentRevisions

        var keyHash = key.ReadExpectEnd<Hash128>();
        var rev = value.ReadExpectEnd<CurrentRevision>();

        if (dump.Csv != null)
        {
            dump.Csv.Write(
                $"{keyHash},{rev.artifactKey.guid},{rev.artifactKey.importerId.NativeImporterType},"+
                $"{rev.artifactKey.importerId.ScriptedImporterType},{rev.artifactID.value}");
        }
        else
        {
            dump.Json!.WriteStartObject(keyHash.ToString());
                Write(dump, rev.artifactKey);
                dump.Json.WriteString("ArtifactId", rev.artifactID.value.ToString());
            dump.Json.WriteEndObject();
        }
    }

    static void Write(DumpContext dump, in BlobArtifactKey key)
    {
        if (dump.Csv != null)
            dump.Csv.Write($"{key.guid},");
        else
            dump.Json!.WriteString("UnityGuid", key.guid.ToString());

        Write(dump, key.importerId);
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
}
