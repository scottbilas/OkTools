using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Spreads.Buffers;
using Spreads.LMDB;
using UnityEngine;
using UnityEngine.AssetLmdb;

// ReSharper disable InvertIf

namespace OkTools.Unity.AssetDb;

public class SourceAssetLmdb : AssetLmdb
{
    const uint k_expectedDbVersion = 9;

    public SourceAssetLmdb(NPath projectRoot)
        : base(projectRoot.Combine(UnityProjectConstants.SourceAssetDbNPath), k_expectedDbVersion) {}
}

[AttributeUsage(AttributeTargets.Method)]
public class AssetLmdbTableAttribute : Attribute
{
    public AssetLmdbTableAttribute(string tableName, string csvFields)
    {
        TableName = tableName;
        CsvFields = csvFields;
    }

    public string TableName { get; }
    public string CsvFields { get; }

    public static TableDumpSpec[] CreateTableDumpSpecs(Type type) => type
        .GetMethods(BindingFlags.Static | BindingFlags.Public)
        .Select(m =>
            {
                var attr = m.GetCustomAttribute<AssetLmdbTableAttribute>();
                if (attr == null)
                    return null;

                if (m.ReturnType != typeof(void))
                    throw new InvalidOperationException($"Method {m.Name} must return void");

                //$$$ test the rest..

                return new TableDumpSpec(attr.TableName, attr.CsvFields, (c, k, v) => m.Invoke(null, new object[] {c, k, v}));
            })
        .Where(s => s != null)
        .Select(m => m!)
        .ToArray();
}

public static class SourceAssetTables
{
    public static readonly TableDumpSpec[] All = AssetLmdbTableAttribute.CreateTableDumpSpecs(typeof(SourceAssetTables));

    [AssetLmdbTable("GuidPropertyIDToProperty", "UnityGuid,Property,IsInMetaFile,Value0,Value1,...")]
    public static void DumpGuidPropertyIdToProperty(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // GuidDB.cpp: GuidDB::m_GuidPropertyIDToProperty

        var property = PropertyDefinition.Find(ref key);
        if (property == null)
            throw new InvalidDataException($"Unknown property: {Encoding.ASCII.GetString(key.Span)}");

        var unityGuid = key.ReadExpectEnd<UnityGuid>();

        if (dump.Csv != null)
        {
            dump.Csv.Write($"{unityGuid},{property.Name},{property.IsInMetaFile}");

            var stringValue = property.Write(value, dump.Buffer);
            if (stringValue.Length != 0)
                dump.Csv.Write($",{stringValue}");
        }

        if (dump.Json != null)
        {
            dump.Json.WriteString("UnityGuid", unityGuid.ToString());
            dump.Json.WriteString("Property", property.Name);
            dump.Json.WriteBoolean("IsInMetaFile", property.IsInMetaFile);
            property.Write(value, dump.Json, "Value");
        }
    }

    [AssetLmdbTable("GuidToChildren", "Parent,Hash,Child0,Child1,...")]
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

        if (dump.Json != null)
        {
            dump.Json.WriteString("UnityGuid", unityGuid.ToString());
            dump.Json.WriteString("Hash", hash.ToString());

            dump.Json.WriteStartArray("Children");
            foreach (var child in children)
                dump.Json.WriteStringValue(child.ToString());
            dump.Json.WriteEndArray();
        }
    }

    [AssetLmdbTable("GuidToIsDir", "UnityGuid,IsDir")]
    public static void DumpGuidToIsDir(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // GuidDB.cpp: GuidDB::m_pGuidToIsDir

        var unityGuid = key.ReadExpectEnd<UnityGuid>();
        var isDir = value.ReadExpectEnd<byte>() != 0;

        dump.Csv?.Write($"{unityGuid},{isDir}");

        if (dump.Json != null)
        {
            dump.Json.WriteString("UnityGuid", unityGuid.ToString());
            dump.Json.WriteBoolean("IsDir", isDir);
        }
    }

    [AssetLmdbTable("GuidToPath", "UnityGuid,Path")]
    public static void DumpGuidToPath(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // GuidDB.cpp: GuidDB::m_pGuidToPath

        var unityGuid = key.ReadExpectEnd<UnityGuid>();
        var path = value.ToAsciiString();

        dump.Csv?.Write($"{unityGuid},{path}");

        if (dump.Json != null)
        {
            dump.Json.WriteString("UnityGuid", unityGuid.ToString());
            dump.Json.WriteString("Path", path);
        }
    }

    [AssetLmdbTable("hash", "Path,Hash,Time,FileSize,IsUntrusted")]
    public static void DumpPathToHash(DumpContext dump, DirectBuffer key, DirectBuffer value)
    {
        // HashDB.cpp: HashDB::m_pPathToHash

        var path = key.ToAsciiString();
        var hash = value.ReadExpectEnd<HashDBValue>();

        dump.Csv?.Write($"{path},{hash.hash},{new DateTime(hash.time)},{hash.fileSize},{hash.isUntrusted}\n");

        if (dump.Json != null)
        {
            dump.Json.WriteString("Path", path);
            dump.Json.WriteString("Hash", hash.hash.ToString());
            dump.Json.WriteString("Time", new DateTime(hash.time));
            dump.Json.WriteNumber("FileSize", hash.fileSize);
            dump.Json.WriteBoolean("IsUntrusted", hash.isUntrusted);
        }
    }
}

// SourceAssetDB.cpp: SourceAssetDB::m_Misc, with keys from SourceAssetDB.cpp "s_Misc_*" area
public class MiscTable : LmdbTable
{
    public MiscTable(SourceAssetLmdb db) : base(db, "Misc") {}

    public IEnumerable<(MiscDefinition, DirectBuffer)> SelectAll(ReadOnlyTransaction tx)
    {
        foreach (var (key, value) in Table.AsEnumerable(tx))
        {
            var found = MiscDefinition.All.FirstOrDefault(miscDef => key.Span[..^1].SequenceEqual(miscDef.NameBuffer));
            if (found == null)
                continue; //throw new InvalidDataException($"Unknown misc entry: {Encoding.ASCII.GetString(key.Span)}");

            yield return (found, value);
        }
    }
}

// GuidDB.cpp: GuidDB::m_pPathToGuid; string path -> GuidDBValue
public class PathToGuidTable : LmdbTable
{
    public PathToGuidTable(SourceAssetLmdb db) : base(db, "PathToGuid") {}

    public IEnumerable<(string, GuidDBValue)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ToAsciiString(),
            kvp.Value.ReadExpectEnd<GuidDBValue>()));
}

// SourceAssetDB.cpp: SourceAssetDB::m_PropertyIDToType; string property -> string type
public class PropertyIdToTypeTable : LmdbTable
{
    /* PropertyDefinition.cpp
    template<> const char* const SInt64PropertyDefinition::s_TypeName = "i64";
    template<> const char* const SInt32PropertyDefinition::s_TypeName = "i32";
    template<> const char* const Hash128PropertyDefinition::s_TypeName = "hash128";
    template<> const char* const StringPropertyDefinition::s_TypeName = "str";
    template<> const char* const StringRefPropertyDefinition::s_TypeName = "str";
    template<> const char* const StringArrayPropertyDefinition::s_TypeName = "str[]";
    template<> const char* const StringRefArrayPropertyDefinition::s_TypeName = "str[]";
    template<> const char* const ImporterIDPropertyDefinition::s_TypeName = "imp";
    template<> const char* const SInt32PairPropertyDefinition::s_TypeName = "pair<i32,i32>";
    */

    public PropertyIdToTypeTable(SourceAssetLmdb db) : base(db, "PropertyIDToType") {}

    public IEnumerable<(string, string)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ToAsciiString(),
            kvp.Value.ToAsciiString()));
}

// SourceAssetDB.cpp: SourceAssetDB::m_RootFolders
public class RootFoldersTable : LmdbTable
{
    public RootFoldersTable(SourceAssetLmdb db) : base(db, "RootFolders") {}

    public IEnumerable<(string, RootFolderProperties)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ToAsciiString(),
            ReadRootFolderProperties(kvp.Value)));

    unsafe RootFolderProperties ReadRootFolderProperties(DirectBuffer value)
    {
        var blob = value.Cast<RootFolderPropertiesBlob>();
        return new RootFolderProperties
        {
            Guid = blob->Guid,
            Immutable = blob->Immutable,
            MountPoint = blob->MountPoint.GetStringFromBlob(),
            Folder = blob->Folder.GetStringFromBlob(),
            PhysicalPath = blob->PhysicalPath.GetStringFromBlob(),
        };
        // TODO: add safety check that the end of PhysicalPath is the end of the value too
    }
}
