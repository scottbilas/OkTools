using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Spreads.Buffers;
using UnityEngine;

// ReSharper disable InvertIf

namespace OkTools.Unity.AssetDb;

public static class SourceAssetLmdb
{
    const uint k_expectedDbVersion = 9;
    public static AssetLmdb OpenLmdb(NPath projectRoot) =>
        new(projectRoot.Combine(UnityProjectConstants.SourceAssetDbNPath), k_expectedDbVersion);

    public static readonly TableDumpSpec[] All = AssetLmdbTableAttribute.CreateTableDumpSpecs(typeof(SourceAssetLmdb));

    [AssetLmdbTable("GuidPropertyIDToProperty", "UnityGuid,Property,IsInMetaFile,Value0,Value1,...")]
    public static void DumpGuidPropertyIdToProperty(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // GuidDB.cpp: GuidDB::m_GuidPropertyIDToProperty

        var (property, unityGuid) = PropertyDefinition.Get<UnityGuid>(key);

        if (dump.Csv != null)
            dump.Csv.Write($"{unityGuid},{property.Name},{property.IsInMetaFile},");
        else
        {
            dump.Json!.WriteString("Property", property.Name);
            dump.Json.WriteString("UnityGuid", unityGuid.ToString());
            dump.Json.WriteBoolean("IsInMetaFile", property.IsInMetaFile);
        }

        LmdbValue.Dump(dump, property.ValueType, ref value);
        value.ExpectEnd();
    }

    [AssetLmdbTable("GuidToChildren", "Parent,Hash,Child0,Child1,...", UniqueKeys = true)]
    public static void DumpGuidToChildren(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // GuidDB.cpp: GuidDB::m_pGuidToChildren

        var unityGuid = key.ReadExpectEnd<UnityGuid>();

        var hash = value.Read<Hash128>();

        // count determined by using remaining space
        var remain = value.Length;
        var count = remain / UnityGuid.SizeOf;
        if (count * UnityGuid.SizeOf != remain)
            throw new InvalidOperationException("Size mismatch");

        var children = MemoryMarshal.Cast<byte, UnityGuid>(value.Span);

        if (dump.Csv != null)
        {
            dump.Csv.Write($"{unityGuid},{hash}");
            foreach (var child in children)
                dump.Csv.Write($",{child}");
        }
        else
        {
            dump.Json!.WriteStartObject(unityGuid.ToString());
            dump.Json.WriteString("Hash", hash.ToString());

            dump.Json.WriteStartArray("Children");
            foreach (var child in children)
                dump.Json.WriteStringValue(child.ToString());
            dump.Json.WriteEndArray();

            dump.Json.WriteEndObject();
        }
    }

    [AssetLmdbTable("GuidToIsDir", "UnityGuid,IsDir", UniqueKeys = true)]
    public static void DumpGuidToIsDir(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // GuidDB.cpp: GuidDB::m_pGuidToIsDir

        var unityGuid = key.ReadExpectEnd<UnityGuid>();
        var isDir = value.ReadExpectEnd<byte>() != 0;

        if (dump.Csv != null)
            dump.Csv.Write($"{unityGuid},{isDir}");
        else
            dump.Json!.WriteBoolean(unityGuid.ToString(), isDir);
    }

    [AssetLmdbTable("GuidToPath", "UnityGuid,Path", UniqueKeys = true)]
    public static void DumpGuidToPath(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // GuidDB.cpp: GuidDB::m_pGuidToPath

        var unityGuid = key.ReadExpectEnd<UnityGuid>();
        var path = value.ToAsciiString();

        if (dump.Csv != null)
            dump.Csv.Write($"{unityGuid},{path}");
        else
            dump.Json!.WriteString(unityGuid.ToString(), path);
    }

    [AssetLmdbTable("hash", "Path,Hash,Time,FileSize,IsUntrusted", UniqueKeys = true)]
    public static void DumpPathToHash(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // HashDB.cpp: HashDB::m_pPathToHash

        var path = key.ToAsciiString();
        var hash = value.ReadExpectEnd<HashDbValue>();

        if (dump.Csv != null)
            dump.Csv.Write($"{path},{hash.Hash},{hash.TimeAsDateTime},{hash.FileSize},{hash.IsUntrusted}");
        else
        {
            dump.Json!.WriteStartObject(path);

            dump.Json.WriteString("Hash", hash.Hash.ToString());
            dump.Json.WriteString("Time", hash.TimeAsDateTime);
            dump.Json.WriteNumber("FileSize", hash.FileSize);
            dump.Json.WriteBoolean("IsUntrusted", hash.IsUntrusted);

            dump.Json.WriteEndObject();
        }
    }

    [AssetLmdbTable("Misc", "Name,Value0,Value1,...", UniqueKeys = true)]
    public static void DumpMisc(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // SourceAssetDB.cpp: SourceAssetDB::m_Misc

        var misc = MiscDefinition.Get(key);

        LmdbValue.Dump(dump, misc.ValueType, ref value, misc.Name);
        value.ExpectEnd();

    }

    [AssetLmdbTable("PathToGuid", "Path,UnityGuid,MetaFileHash,AssetFileHash", UniqueKeys = true)]
    public static void DumpPathToGuid(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // GuidDB.cpp: GuidDB::m_pPathToGuid

        var path = key.ToAsciiString();
        var guidValue = value.ReadExpectEnd<GuidDbValue>();

        if (dump.Csv != null)
            dump.Csv.Write($"{path},{guidValue.Guid},{guidValue.MetaFileHash},{guidValue.AssetFileHash}");
        else
        {
            dump.Json!.WriteStartObject(path);
            dump.Json.WriteString("UnityGuid", guidValue.Guid.ToString());
            dump.Json.WriteString("MetaFileHash", guidValue.MetaFileHash.ToString());
            dump.Json.WriteString("AssetFileHash", guidValue.AssetFileHash.ToString());
            dump.Json.WriteEndObject();
        }
    }

    [AssetLmdbTable("PropertyIDToType", "PropertyId,Type", UniqueKeys = true)]
    public static void DumpPropertyIdToType(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // SourceAssetDB.cpp: SourceAssetDB::m_PropertyIDToType
        // Also see PropertyDefinition.cpp for where these entries come from

        var propertyId = key.ToAsciiString();
        var typeName = value.ToAsciiString();

        if (dump.Csv != null)
            dump.Csv.Write($"{propertyId},{typeName}");
        else
            dump.Json!.WriteString(propertyId, typeName);
    }

    [AssetLmdbTable("RootFolders", "RootFolder,UnityGuid,Immutable,MountPoint,Folder,PhysicalPath", UniqueKeys = true)]
    public static unsafe void DumpRootFolders(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // SourceAssetDB.cpp: SourceAssetDB::m_RootFolders

        var rootFolder = key.ToAsciiString();

        var blob = value.Cast<RootFolderPropertiesBlob>();
        var unityGuid = blob->Guid;
        var immutable = blob->Immutable;
        var mountPoint = blob->MountPoint;
        var folder = blob->Folder;
        var physicalPath = blob->PhysicalPath;

        if (dump.Csv != null)
            dump.Csv.Write($"{rootFolder},{unityGuid},{immutable},{mountPoint},{folder},{physicalPath}");
        else
        {
            dump.Json!.WriteStartObject(rootFolder);
                dump.Json.WriteString("UnityGuid", unityGuid.ToString());
                dump.Json.WriteBoolean("Immutable", immutable);
                dump.Json.WriteString("MountPoint", mountPoint.ToString());
                dump.Json.WriteString("Folder", folder.ToString());
                dump.Json.WriteString("PhysicalPath", physicalPath.ToString());
            dump.Json.WriteEndObject();
        }

        // TODO: add safety check that the end of PhysicalPath is the end of the value too
    }
}
