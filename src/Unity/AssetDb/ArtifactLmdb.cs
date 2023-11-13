using Spreads.Buffers;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace OkTools.Unity.AssetDb;

static class ArtifactLmdb
{
    static readonly uint[] k_expectedDbVersions = { 0x5CE21767, 0x01F5F63B };

    public static AssetLmdb OpenLmdb(NPath projectRoot) =>
        new(projectRoot.Combine(UnityProjectConstants.ArtifactDbNPath), k_expectedDbVersions);

    public static readonly TableDumpSpec[] All = AssetLmdbTableAttribute.CreateTableDumpSpecs(typeof(ArtifactLmdb));

    [AssetLmdbTable("ArtifactIDPropertyIDToProperty", "ArtifactID,Property,ValueType,IsInMetaFile,Value0,Value1,...")]
    public static void DumpArtifactIdPropertyIdToProperty(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // ArtifactDB.cpp: ArtifactDB::m_ArtifactIDPropertyIDToProperty

        var (property, id) = PropertyDefinition.Get<ArtifactID>(key);

        if (dump.Csv != null)
            dump.Csv.Write($"{id.value},{property.Name},{property.ValueType},");
        else
        {
            dump.Json!.WriteString("ArtifactID", id.value.ToString());
            dump.Json.WriteString("Property", property.Name);
            dump.Json.WriteString("ValueType", property.ValueType.ToString());
        }

        LmdbValue.Dump(dump, property.ValueType, ref value);
        value.ExpectEnd();
    }

    [AssetLmdbTable("ArtifactIDToArtifactDependencies", "ArtifactID,DependenciesHash,StaticDependencyHash,(Check json for the rest!)", UniqueKeys = true)]
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
                dump.Json.WriteString("dependenciesHash", deps->dependenciesHash.value.ToString());
                dump.Json.WriteString("staticDependencyHash", deps->staticDependencyHash.value.ToString());

                // this is just redundant with the key, skip it
                //dump.Json.WriteString("ArtifactID", deps->artifactID.value.ToString());

                dump.Json.WriteStartObject("staticDependencies");
                {
                    var staticDependencies = deps->staticDependencies.Ptr;

                    dump.Json.WriteNumber("artifactFormatVersion", staticDependencies->artifactFormatVersion);
                    dump.Json.WriteNumber("allImporterVersion", staticDependencies->allImporterVersion);
                    Write(dump, staticDependencies->importerID, "importerID");
                    dump.Json.WriteNumber("importerVersion", staticDependencies->importerVersion);
                    if (!dump.Config.OptTrim || staticDependencies->postprocessorType != PostprocessorType.kPostprocessorNone)
                        dump.Json.WriteString("postprocessorType", staticDependencies->postprocessorType.ToString());
                    if (!dump.Config.OptTrim || staticDependencies->postprocessorVersionHash.IsValid)
                        dump.Json.WriteString("postprocessorVersionHash", staticDependencies->postprocessorVersionHash.ToString());
                    dump.Json.WriteString("nameOfAsset", staticDependencies->nameOfAsset.GetString());
                    dump.Json.WriteStartArray("hashOfSourceAsset");
                        for (var i = 0; i < staticDependencies->hashOfSourceAsset.Length; i++)
                            Write(dump, *staticDependencies->hashOfSourceAsset.PtrAt(i), null);
                    dump.Json.WriteEndArray();
                }
                dump.Json.WriteEndObject();

                // dynamicDependencies
                {
                    var dynamicDependencies = deps->dynamicDependencies.Ptr;
                    var openedDynamicDependencies = false;

                    bool StartArray<T>(string name, BlobArray<T> array) where T : unmanaged
                    {
                        if (dump.Config.OptTrim && array.Length == 0)
                            return false;

                        if (!openedDynamicDependencies)
                        {
                            dump.Json.WriteStartObject("dynamicDependencies");
                            openedDynamicDependencies = true;
                        }

                        dump.Json.WriteStartArray(name);
                        return true;
                    }

                    if (StartArray("hashOfSourceAsset", dynamicDependencies->hashOfSourceAsset))
                    {
                        for (var i = 0; i < dynamicDependencies->hashOfSourceAsset.Length; ++i)
                            Write(dump, *dynamicDependencies->hashOfSourceAsset.PtrAt(i), null);
                        dump.Json.WriteEndArray();
                    }

                    if (StartArray("guidOfPathLocation", dynamicDependencies->guidOfPathLocation))
                    {
                        for (var i = 0; i < dynamicDependencies->guidOfPathLocation.Length; ++i)
                        {
                            var guidOfPathLocation = dynamicDependencies->guidOfPathLocation.PtrAt(i);
                            dump.Json.WriteStartObject();
                                dump.Json.WriteString("path", guidOfPathLocation->path.GetString());
                                dump.Json.WriteString("guid", guidOfPathLocation->guid.ToString());
                            dump.Json.WriteEndObject();
                        }
                        dump.Json.WriteEndArray();
                    }

                    if (StartArray("hashOfGUIDsOfChildren", dynamicDependencies->hashOfGUIDsOfChildren))
                    {
                        for (var i = 0; i < dynamicDependencies->hashOfGUIDsOfChildren.Length; ++i)
                        {
                            var hashOfGUIDsOfChildren = dynamicDependencies->hashOfGUIDsOfChildren.PtrAt(i);
                            dump.Json.WriteStartObject();
                                dump.Json.WriteString("path", hashOfGUIDsOfChildren->guid.ToString());
                                dump.Json.WriteString("guid", hashOfGUIDsOfChildren->hash.ToString());
                            dump.Json.WriteEndObject();
                        }
                        dump.Json.WriteEndArray();
                    }

                    if (StartArray("hashOfArtifact", dynamicDependencies->hashOfArtifact))
                    {
                        for (var i = 0; i < dynamicDependencies->hashOfArtifact.Length; ++i)
                        {
                            var hashOfArtifact = dynamicDependencies->hashOfArtifact.PtrAt(i);
                            dump.Json.WriteStartObject();
                                Write(dump, hashOfArtifact->artifactKey, "artifactKey");
                                dump.Json.WriteString("artifactID", hashOfArtifact->artifactID.value.ToString());
                            dump.Json.WriteEndObject();
                        }
                        dump.Json.WriteEndArray();
                    }

                    if (StartArray("propertyOfArtifact", dynamicDependencies->propertyOfArtifact))
                    {
                        for (var i = 0; i < dynamicDependencies->propertyOfArtifact.Length; ++i)
                        {
                            var propertyOfArtifact = dynamicDependencies->propertyOfArtifact.PtrAt(i);
                            dump.Json.WriteStartObject();
                                Write(dump, propertyOfArtifact->artifactKey, "artifactKey");
                                Write(dump, &propertyOfArtifact->prop, "prop");
                            dump.Json.WriteEndObject();
                        }
                        dump.Json.WriteEndArray();
                    }

                    if (!dump.Config.OptTrim || dynamicDependencies->buildTarget.HasValue)
                        dump.Json.WriteString("buildTarget", dynamicDependencies->buildTarget.ToString());
                    if (!dump.Config.OptTrim || dynamicDependencies->buildTargetPlatformGroup.HasValue)
                        dump.Json.WriteString("buildTargetPlatformGroup", dynamicDependencies->buildTargetPlatformGroup.ToString());
                    if (!dump.Config.OptTrim || dynamicDependencies->textureImportCompression.HasValue)
                        dump.Json.WriteString("textureImportCompression", dynamicDependencies->textureImportCompression.ToString());
                    if (!dump.Config.OptTrim || dynamicDependencies->colorSpace.HasValue)
                        dump.Json.WriteString("colorSpace", dynamicDependencies->colorSpace.ToString());
                    if (!dump.Config.OptTrim || dynamicDependencies->graphicsApiMask.HasValue)
                        dump.Json.WriteString("graphicsApiMask", dynamicDependencies->graphicsApiMask.ToString());
                    if (!dump.Config.OptTrim || dynamicDependencies->scriptingRuntimeVersion.HasValue)
                        dump.Json.WriteString("scriptingRuntimeVersion", dynamicDependencies->scriptingRuntimeVersion.ToString());

                    if (StartArray("customDependencies", dynamicDependencies->customDependencies))
                    {
                        for (var i = 0; i < dynamicDependencies->customDependencies.Length; ++i)
                        {
                            var customDependency = dynamicDependencies->customDependencies.PtrAt(i);
                            dump.Json.WriteStartObject();
                                dump.Json.WriteString("name", customDependency->name.GetString());
                                dump.Json.WriteString("valueHash", customDependency->valueHash.ToString());
                            dump.Json.WriteEndObject();
                        }
                        dump.Json.WriteEndArray();
                    }

                    if (openedDynamicDependencies)
                        dump.Json.WriteEndObject();
                    else
                        dump.Json.WriteString("dynamicDependencies", "(none)");
                }
            }
            dump.Json.WriteEndObject();
        }
    }

    [AssetLmdbTable("ArtifactIDToArtifactMetaInfo", "", UniqueKeys = true)]
    public static unsafe void DumpArtifactIdToArtifactMetaInfo(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // ArtifactDB.cpp: ArtifactDB::m_ArtifactIDToArtifactMetaInfo

        var id = key.ReadExpectEnd<ArtifactID>();
        var stats = value.Cast<ArtifactMetaInfo>();

        if (dump.Csv != null)
            dump.Csv.Write($"{id.value},{stats->artifactMetaInfoHash.value},");
        else
        {
            dump.Json!.WriteStartObject(id.value.ToString());
            dump.Json.WriteString("artifactMetaInfoHash", stats->artifactMetaInfoHash.value.ToString());
        }

        Write(dump, stats->artifactKey, "artifactKey");

        if (dump.Csv != null)
            dump.Csv.Write($",{stats->type},{stats->isImportedAssetCacheable}");
        else
        {
            dump.Json!.WriteString("type", stats->type.ToString());
            dump.Json.WriteBoolean("isImportedAssetCacheable", stats->isImportedAssetCacheable);

            dump.Json.WriteStartArray("producedFiles");
            for (var i = 0; i < stats->producedFiles.Length; ++i)
            {
                var producedFile = stats->producedFiles.PtrAt(i);
                dump.Json.WriteStartObject();
                    dump.Json.WriteString("storage", producedFile->storage.ToString());
                    var extension = producedFile->extension.GetString();
                    if (!dump.Config.OptTrim || extension.Length != 0)
                        dump.Json.WriteString("extension", extension);
                    dump.Json.WriteString("contentHash", producedFile->contentHash.ToString());
                    if (!dump.Config.OptTrim || producedFile->inlineStorage.Length != 0)
                        dump.Json.WriteString("inlineStorage", $"({producedFile->inlineStorage.Length} bytes)");
                dump.Json.WriteEndObject();
            }
            dump.Json.WriteEndArray();

            dump.Json.WriteStartArray("properties");
            for (var i = 0; i < stats->properties.Length; ++i)
                Write(dump, stats->properties.PtrAt(i), null);
            dump.Json.WriteEndArray();

            dump.Json.WriteStartArray("importedAssetMetaInfos");
            for (var i = 0; i < stats->importedAssetMetaInfos.Length; ++i)
            {
                var metaInfo = stats->importedAssetMetaInfos.PtrAt(i);
                dump.Json.WriteStartObject();
                    dump.Json.WriteBoolean("postProcessedAsset", metaInfo->postProcessedAsset);
                    Write(dump, &metaInfo->mainObjectInfo, "mainObjectInfo");

                    dump.Json.WriteStartArray("objectInfo");
                    for (var j = 0; j < metaInfo->objectInfo.Length; ++j)
                        Write(dump, metaInfo->objectInfo.PtrAt(j), null);
                    dump.Json.WriteEndArray();
                dump.Json.WriteEndObject();
            }
            dump.Json.WriteEndArray();

            dump.Json.WriteEndObject();
        }
    }

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
            dump.Json!.WriteString("guid", key.guid.ToString());

        Write(dump, key.importerId, "importerId");
        dump.Json?.WriteEndObject();
    }

    static void Write(DumpContext dump, in ImporterId importerId, string objectName)
    {
        if (dump.Csv != null)
            dump.Csv.Write($"{importerId.NativeImporterType},{importerId.ScriptedImporterType}");
        else if (dump.Config.OptTrim && importerId.NativeImporterType == -1 && !importerId.ScriptedImporterType.IsValid)
            dump.Json!.WriteString(objectName, "(default)");
        else
        {
            dump.Json!.WriteStartObject(objectName);
                dump.Json.WriteNumber("NativeImporterType", importerId.NativeImporterType);
                dump.Json.WriteString("ScriptedImporterType", importerId.ScriptedImporterType.ToString());
            dump.Json.WriteEndObject();
        }
    }

    static unsafe void Write(DumpContext dump, BlobProperty* property, string? objectName)
    {
        if (dump.Csv != null)
            dump.Csv.Write($"{property->id.GetString()},({property->data.Length} bytes)");
        else
        {
            if (objectName != null)
                dump.Json!.WriteStartObject(objectName);
            else
                dump.Json!.WriteStartObject();

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

    static unsafe void Write(DumpContext dump, ImportedObjectMetaInfo* metaInfo, string? objectName)
    {
        if (dump.Csv != null)
            dump.Csv.Write($"{metaInfo->name.GetString()},{metaInfo->typeID},{metaInfo->flags},{metaInfo->scriptClassName.name.GetString()},{metaInfo->localIdentifier}");
        else
        {
            if (objectName != null)
                dump.Json!.WriteStartObject(objectName);
            else
                dump.Json!.WriteStartObject();

            dump.Json.WriteString("name", metaInfo->name.GetString());
            if (!dump.Config.OptTrim || metaInfo->thumbnail.format != GraphicsFormat.kFormatNone)
            {
                dump.Json.WriteStartObject("thumbnail");
                    dump.Json.WriteString("format", metaInfo->thumbnail.format.ToString());
                    dump.Json.WriteNumber("width", metaInfo->thumbnail.width);
                    dump.Json.WriteNumber("height", metaInfo->thumbnail.height);
                    dump.Json.WriteNumber("rowBytes", metaInfo->thumbnail.rowBytes);
                    dump.Json.WriteString("image", $"({metaInfo->thumbnail.image.Length} bytes)");
                dump.Json.WriteEndObject();
            }
            dump.Json.WriteNumber("typeID", metaInfo->typeID);
            dump.Json.WriteNumber("flags", metaInfo->flags);

            var openedScriptClassName = false;

            void StartScriptClassName()
            {
                if (!openedScriptClassName)
                {
                    dump.Json!.WriteStartObject("scriptClassName");
                    openedScriptClassName = true;
                }
            }

            var scriptName = metaInfo->scriptClassName.name.GetString();
            if (!dump.Config.OptTrim || scriptName.Length != 0)
            {
                StartScriptClassName();
                dump.Json.WriteString("name", scriptName);
            }

            var monoScript = metaInfo->scriptClassName.monoScript;
            if (!dump.Config.OptTrim || monoScript.guid.IsValid() || monoScript.localIdentifier != 0 || monoScript.type != FileIdentifierType.kInvalidType)
            {
                StartScriptClassName();

                dump.Json.WriteStartObject("monoScript");
                    dump.Json.WriteString("guid", monoScript.guid.ToString());
                    dump.Json.WriteNumber("localIdentifier", monoScript.localIdentifier);
                    dump.Json.WriteString("type", monoScript.type.ToString());
                dump.Json.WriteEndObject();
            }

            if (openedScriptClassName)
                dump.Json.WriteEndObject();

            dump.Json.WriteNumber("localIdentifier", metaInfo->localIdentifier);

            dump.Json.WriteEndObject();
        }
    }
}
