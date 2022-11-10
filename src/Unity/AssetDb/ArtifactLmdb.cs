using Spreads.Buffers;
using UnityEngine;

// ReSharper disable InconsistentNaming

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

    [AssetLmdbTable("ArtifactIDToArtifactDependencies", "ArtifactID,DependenciesHash,StaticDependencyHash # check json for the rest (it's complex)")]
    public static unsafe void DumpArtifactIdToArtifactDependencies(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // ArtifactDB.cpp: ArtifactDB::m_ArtifactIDToArtifactDependencies

        var id = key.Read<ArtifactID>();
        var deps = value.Cast<ArtifactDependencies>();

        if (dump.Csv != null)
            dump.Csv.Write($"{id.value},{deps->dependenciesHash.value},{deps->staticDependencyHash.value}");
        else
        {
            dump.Json!.WriteStartObject(id.value.ToString());
            {
                dump.Json.WriteString("DependenciesHash", deps->dependenciesHash.value.ToString());
                dump.Json.WriteString("StaticDependencyHash", deps->staticDependencyHash.value.ToString());

                // this is just redundant with the key, skip it
                //dump.Json.WriteString("ArtifactID", deps->artifactID.value.ToString());

                dump.Json.WriteStartObject("StaticDependencies");
                {
                    var staticDependencies = deps->staticDependencies.Ptr;

                    dump.Json.WriteNumber("ArtifactFormatVersion", staticDependencies->artifactFormatVersion);
                    dump.Json.WriteNumber("AllImporterVersion", staticDependencies->allImporterVersion);
                    Write(dump, staticDependencies->importerID, "importerID");
                    dump.Json.WriteNumber("ImporterVersion", staticDependencies->importerVersion);
                    dump.Json.WriteString("PostprocessorType", staticDependencies->postprocessorType.ToString());
                    dump.Json.WriteString("PostprocessorVersionHash", staticDependencies->postprocessorVersionHash.ToString());
                    dump.Json.WriteString("NameOfAsset", staticDependencies->nameOfAsset.GetString());
                    dump.Json.WriteStartArray("HashOfSourceAsset");
                        for (var i = 0; i < staticDependencies->hashOfSourceAsset.Length; i++)
                            Write(dump, *staticDependencies->hashOfSourceAsset.PtrAt(i), null);
                    dump.Json.WriteEndArray();
                }
                dump.Json.WriteEndObject();

                dump.Json.WriteStartObject("DynamicDependencies");
                {
                    var dynamicDependencies = deps->dynamicDependencies.Ptr;

                    dump.Json.WriteStartArray("HashOfSourceAsset");
                        for (var i = 0; i < dynamicDependencies->hashOfSourceAsset.Length; ++i)
                            Write(dump, *dynamicDependencies->hashOfSourceAsset.PtrAt(i), null);
                    dump.Json.WriteEndArray();

                    dump.Json.WriteStartArray("GuidOfPathLocation");
                        for (var i = 0; i < dynamicDependencies->guidOfPathLocation.Length; ++i)
                        {
                            var guidOfPathLocation = dynamicDependencies->guidOfPathLocation.PtrAt(i);
                            dump.Json.WriteStartObject();
                                dump.Json.WriteString("path", guidOfPathLocation->path.GetString());
                                dump.Json.WriteString("guid", guidOfPathLocation->guid.ToString());
                            dump.Json.WriteEndObject();
                        }
                    dump.Json.WriteEndArray();

                    dump.Json.WriteStartArray("HashOfGUIDsOfChildren");
                        for (var i = 0; i < dynamicDependencies->hashOfGUIDsOfChildren.Length; ++i)
                        {
                            var hashOfGUIDsOfChildren = dynamicDependencies->hashOfGUIDsOfChildren.PtrAt(i);
                            dump.Json.WriteStartObject();
                                dump.Json.WriteString("path", hashOfGUIDsOfChildren->guid.ToString());
                                dump.Json.WriteString("guid", hashOfGUIDsOfChildren->hash.ToString());
                            dump.Json.WriteEndObject();
                        }
                    dump.Json.WriteEndArray();

                    dump.Json.WriteStartArray("HashOfArtifact");
                        for (var i = 0; i < dynamicDependencies->hashOfArtifact.Length; ++i)
                        {
                            var hashOfArtifact = dynamicDependencies->hashOfArtifact.PtrAt(i);
                            dump.Json.WriteStartObject();
                                Write(dump, hashOfArtifact->artifactKey, "artifactKey");
                                dump.Json.WriteString("artifactID", hashOfArtifact->artifactID.value.ToString());
                            dump.Json.WriteEndObject();
                        }
                    dump.Json.WriteEndArray();

                    dump.Json.WriteStartArray("PropertyOfArtifact");
                        for (var i = 0; i < dynamicDependencies->propertyOfArtifact.Length; ++i)
                        {
                            var propertyOfArtifact = dynamicDependencies->propertyOfArtifact.PtrAt(i);
                            dump.Json.WriteStartObject();
                                Write(dump, propertyOfArtifact->artifactKey, "artifactKey");
                                Write(dump, &propertyOfArtifact->prop, "prop");
                            dump.Json.WriteEndObject();
                        }
                    dump.Json.WriteEndArray();

    /*    public BlobOptional<BuildTargetSelection>     buildTarget;
        public BlobOptional<BuildTargetPlatformGroup> buildTargetPlatformGroup;
        public BlobOptional<TextureImportCompression> textureImportCompression;
        public BlobOptional<ColorSpace>               colorSpace;
        public BlobOptional<UInt32>                   graphicsApiMask;
        public BlobOptional<ScriptingRuntimeVersion>  scriptingRuntimeVersion;
        public BlobArray<CustomDependency>            customDependencies;*/
                }
                dump.Json.WriteEndObject();
            }
            dump.Json.WriteEndObject();
        }
    }

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

        Write(dump, valueBlob->artifactKey, "artifactKey");

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
                Write(dump, rev.artifactKey, "artifactKey");
                dump.Json.WriteString("ArtifactId", rev.artifactID.value.ToString());
            dump.Json.WriteEndObject();
        }
    }

    static void Write(DumpContext dump, in BlobArtifactKey key, string objectName)
    {
        dump.Json?.WriteStartObject(objectName);

        if (dump.Csv != null)
            dump.Csv.Write($"{key.guid},");
        else
            dump.Json!.WriteString("UnityGuid", key.guid.ToString());

        Write(dump, key.importerId, "importerId");
        dump.Json?.WriteEndObject();
    }

    static void Write(DumpContext dump, in ImporterId importerId, string objectName)
    {
        if (dump.Csv != null)
            dump.Csv.Write($"{importerId.NativeImporterType},{importerId.ScriptedImporterType}");
        else
        {
            dump.Json!.WriteStartObject(objectName);
                dump.Json.WriteNumber("NativeImporterType", importerId.NativeImporterType);
                dump.Json.WriteString("ScriptedImporterType", importerId.ScriptedImporterType.ToString());
            dump.Json.WriteEndObject();
        }
    }

    static unsafe void Write(DumpContext dump, BlobProperty* property, string objectName)
    {
        if (dump.Csv != null)
            dump.Csv.Write($"{property->id.GetString()},({property->data.Length} bytes)");
        else
        {
            dump.Json!.WriteStartObject(objectName);
                dump.Json.WriteString("id", property->id.GetString());
                dump.Json.WriteString("data", $"({property->data.Length} bytes)");
            dump.Json.WriteEndObject();
        }
    }

    static void Write(DumpContext dump, in HashOfSourceAsset hashOfSourceAsset, string? objectName)
    {
        if (dump.Csv != null)
            dump.Csv.Write($"{hashOfSourceAsset.guid},{hashOfSourceAsset.assetHash},{hashOfSourceAsset.metaFileHash}");
        else
        {
            if (objectName != null)
                dump.Json!.WriteStartObject(objectName);
            else
                dump.Json!.WriteStartObject();

            dump.Json.WriteString("guid", hashOfSourceAsset.guid.ToString());
            dump.Json.WriteString("assetHash", hashOfSourceAsset.assetHash.ToString());
            dump.Json.WriteString("metaFileHash", hashOfSourceAsset.metaFileHash.ToString());

            dump.Json.WriteEndObject();
        }
    }
}
