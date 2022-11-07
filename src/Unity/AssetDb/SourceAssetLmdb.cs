using System.Runtime.InteropServices;
using System.Text;
using Spreads.Buffers;
using Spreads.LMDB;
using UnityEngine;
using UnityEngine.AssetLmdb;

// ReSharper disable BuiltInTypeReferenceStyle

namespace OkTools.Unity.AssetDb;

public class SourceAssetLmdb : AssetLmdb
{
    const uint k_expectedDbVersion = 9;

    public SourceAssetLmdb(NPath projectRoot)
        : base(projectRoot.Combine(UnityProjectConstants.SourceAssetDbNPath), k_expectedDbVersion) {}
}

public class PropertyDefinition
{
    readonly PropertyType _propertyType;

    public readonly string Prefix;
    public readonly DirectBuffer PrefixBuffer;
    public readonly bool IsInMetaFile;

    PropertyDefinition(string prefix, bool isInMetaFile, PropertyType type)
    {
        Prefix = prefix;
        PrefixBuffer = LmdbUtils.StringToBuffer(prefix, false);
        IsInMetaFile = isInMetaFile;

        _propertyType = type;
    }

    public static readonly PropertyDefinition[] All =
    {
        // from top of PropertyDefinition.cpp
        /*StringArrayPropertyDefinition kLabelsPropDef*/                     new("labels",                          true,  PropertyType.StringArray),
        /*SInt32PropertyDefinition kAssetBundleIndexPropDef*/                new("AssetBundleIndex",                false, PropertyType.SInt32),
        /*StringPropertyDefinition kAssetBundleNamePropDef*/                 new("assetBundleName",                 true,  PropertyType.String),
        /*StringPropertyDefinition kAssetBundleVariantPropDef*/              new("assetBundleVariant",              true,  PropertyType.String),
        /*SInt64PropertyDefinition kMainObjectLocalIdentifierInFilePropDef*/ new("MainObjectLocalIdentifierInFile", false, PropertyType.SInt64),
        /*ImporterIDPropertyDefinition kImporterOverridePropDef*/            new("importerOverride",                true,  PropertyType.ImporterId),
        /*Hash128PropertyDefinition kImportLogFilePropDef*/                  new("importLogFile",                   false, PropertyType.Hash128),
        /*SInt32PairPropertyDefinition kImportLogEntriesCountPropDef*/       new("ImportLogEntriesCount",           false, PropertyType.SInt32Pair),
        /*StringPropertyDefinition kScriptCompilationAssetPathPropDef*/      new("scriptCompilationAssetPath",      false, PropertyType.String),
        /*Hash128PropertyDefinition kImporterErrorFilePropDef*/              new("importerErrorFile",               false, PropertyType.Hash128),
        /*SInt32PropertyDefinition kAssetOriginProductIdPropDef*/            new("productId",                       true,  PropertyType.SInt32),
        /*StringPropertyDefinition kAssetOriginPackageNamePropDef*/          new("packageName",                     true,  PropertyType.String),
        /*StringPropertyDefinition kAssetOriginPackageVersionPropDef*/       new("packageVersion",                  true,  PropertyType.String),
        /*StringPropertyDefinition kAssetOriginAssetPathPropDef*/            new("assetPath",                       true,  PropertyType.String),
        /*SInt32PropertyDefinition kAssetOriginUploadIdPropDef*/             new("uploadId",                        true,  PropertyType.SInt32),
    };

    enum PropertyType
    {
        SInt32,
        SInt32Pair,
        SInt64,
        String,
        StringArray,
        ImporterId,
        Hash128,
    }

    public string ToCsv(DirectBuffer value, StringBuilder sb)
    {
        string str;

        // ReSharper disable BuiltInTypeReferenceStyle
        switch (_propertyType)
        {
            case PropertyType.SInt32:
                str = value.Read<Int32>().ToString();
                break;

            case PropertyType.SInt32Pair:
                var i0 = value.Read<Int32>();
                var i1 = value.Read<Int32>();
                str = $"{i0},{i1}";
                break;

            case PropertyType.SInt64:
                str = value.Read<Int64>().ToString();
                break;

            case PropertyType.String:
                str = value.ReadAscii(true);
                break;

            case PropertyType.StringArray:
                var count = value.Read<UInt32>();
                for (var i = 0; i < count; ++i)
                {
                    if (sb.Length != 0)
                        sb.Append(',');

                    var len = value.Read<UInt16>();
                    value.ReadAscii(sb, len, true);
                }
                str = sb.ToString();
                sb.Clear();
                break;

            case PropertyType.ImporterId:
                var id = value.Read<ImporterId>();
                str = $"{id.NativeImporterType},{id.ScriptedImporterType}";
                break;

            case PropertyType.Hash128:
                str = value.Read<Hash128>().ToString();
                break;

            default:
                throw new InvalidOperationException();
        }
        // ReSharper restore BuiltInTypeReferenceStyle

        value.ExpectEnd();
        return str;
    }
}

// GuidDB.cpp: GuidDB::m_GuidPropertyIDToProperty
public class GuidPropertyIdToPropertyTable : LmdbTable
{
    public GuidPropertyIdToPropertyTable(SourceAssetLmdb db) : base(db, "GuidPropertyIDToProperty") {}

    public IEnumerable<(UnityGuid, PropertyDefinition, DirectBuffer)> SelectAll(ReadOnlyTransaction tx)
    {
        foreach (var (key, value) in Table.AsEnumerable(tx))
        {
            var found = PropertyDefinition.All.FirstOrDefault(propertyDef => key.Span.StartsWith(propertyDef.PrefixBuffer.Span));
            if (found == null)
                throw new InvalidDataException($"Unknown property: {Encoding.ASCII.GetString(key.Span)}");

            var unityGuid = key.Slice(found.PrefixBuffer.Length).ReadExpectEnd<UnityGuid>();
            yield return (unityGuid, found, value);
        }
    }
}

// GuidDB.cpp: GuidDB::m_pGuidToChildren; UnityGuid -> GuidChildren
public class GuidToChildrenTable : LmdbTable
{
    public GuidToChildrenTable(SourceAssetLmdb db) : base(db, "GuidToChildren") {}

    public IEnumerable<(UnityGuid, GuidChildren)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ReadExpectEnd<UnityGuid>(),
            ReadChildren(kvp.Value)));

    static GuidChildren ReadChildren(DirectBuffer value)
    {
        var hash = value.Read<Hash128>();

        // count determined by using remaining space
        var remain = value.Length;
        var count = remain / UnityGuid.SizeOf;
        if (count * UnityGuid.SizeOf != remain)
            throw new InvalidOperationException("Size mismatch");

        var children = MemoryMarshal.Cast<byte, UnityGuid>(value.Span);

        // TODO: get rid of alloc (just point at lmdb memory), if this ever gets to the point where we need to care.
        // for now just do what's faster to write.
        return new GuidChildren { hash = hash, guids = children.ToArray() };
    }
}

// GuidDB.cpp: GuidDB::m_pGuidToIsDir; string -> uint8 (bool, 0 or nonzero)
public class GuidToIsDirTable : LmdbTable
{
    public GuidToIsDirTable(SourceAssetLmdb db) : base(db, "GuidToIsDir") {}

    public IEnumerable<(UnityGuid, bool)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ReadExpectEnd<UnityGuid>(),
            kvp.Value.ReadExpectEnd<byte>() != 0));
}

// GuidDB.cpp: GuidDB::m_pGuidToPath; UnityGuid -> string path
public class GuidToPathTable : LmdbTable
{
    public GuidToPathTable(SourceAssetLmdb db) : base(db, "GuidToPath") {}

    public IEnumerable<(UnityGuid, string)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ReadExpectEnd<UnityGuid>(),
            kvp.Value.ToAsciiString()));
}

// HashDB.cpp: HashDB::m_pPathToHash; string path -> HashDBValue
public class PathToHashTable : LmdbTable
{
    public PathToHashTable(SourceAssetLmdb db) : base(db, "hash") {}

    public IEnumerable<(string, HashDBValue)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ToAsciiString(),
            kvp.Value.ReadExpectEnd<HashDBValue>()));
}

public class MiscDefinition
{
    readonly MiscType _miscType;

    public readonly string Name;
    public readonly DirectBuffer NameBuffer;

    MiscDefinition(string name, MiscType type)
    {
        Name = name;
        NameBuffer = LmdbUtils.StringToBuffer(name, false);

        _miscType = type;
    }

    public static readonly MiscDefinition[] All =
    {
        // look for s_Misc_* in SourceAssetDB.cpp
        /*s_Misc_RefreshVersion*/          new("refreshVersion",          MiscType.SInt32),           // refreshVersion -> int version (default -1 if missing)
        /*s_Misc_ShaderCacheClearVersion*/ new("shaderCacheClearVersion", MiscType.SInt32),           // shaderCacheClearVersion -> int version (default -1 if missing)
        /*s_Misc_CrashedImportPaths*/      new("crashedImportPaths",      MiscType.MultilineString),  // crashedImportPaths -> string[] (default empty if missing)
        /*s_AssetBundNames*/               new("assetBundleNames",        MiscType.AssetBundleNames), // assetBundleNames -> BlobArray<AssetBundleFullNameIndex>* (needs parsing to interpret, see SourceAssetDBWriteTxn::AddAssetBundleNames)
    };

    enum MiscType
    {
        SInt32,
        MultilineString,
        AssetBundleNames,
    }

    public unsafe AssetBundleFullNameIndex[]? TryGetAssetBundleNames(DirectBuffer value)
    {
        if (_miscType != MiscType.AssetBundleNames)
            return null;

        var names = value.Cast<BlobArray<AssetBundleFullNameIndexBlob>>();
        var result = new AssetBundleFullNameIndex[names->Length];

        for (var i = 0; i < names->Length; ++i)
        {
            var blob = names->RefElementFromBlob(i);
            result[i] = new AssetBundleFullNameIndex
            {
                AssetBundleName = blob->AssetBundleName.GetStringFromBlob(),
                AssetBundleVariant = blob->AssetBundleVariant.GetStringFromBlob(),
                Index = blob->Index,
            };
        }

        return result;
    }

    public string ToCsv(DirectBuffer value)
    {
        var str = _miscType switch
        {
            MiscType.SInt32 =>
                value.Read<Int32>().ToString(),
            MiscType.MultilineString =>
                value.ReadAscii(true).TrimEnd().Replace('\n', ','),

            _ => throw new InvalidOperationException()
        };

        value.ExpectEnd();
        return str;
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
            var found = MiscDefinition.All.FirstOrDefault(miscDef => key.Span[..^1].SequenceEqual(miscDef.NameBuffer.Span));
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

struct ImporterId
{
    public Int32 NativeImporterType;
    public Hash128 ScriptedImporterType;
}

// SourceAssetDB.h: RootFolderPropertiesBlob
struct RootFolderPropertiesBlob
{
    public UnityGuid Guid;
    public bool Immutable;
    public BlobString MountPoint;
    public BlobString Folder;
    public BlobString PhysicalPath;
}

// TODO: get rid of this alloc-by-default thing
public struct RootFolderProperties
{
    public UnityGuid Guid;
    public bool Immutable;
    public string MountPoint;
    public string Folder;
    public string PhysicalPath;
}

struct AssetBundleFullNameIndexBlob
{
    public BlobString AssetBundleName;
    public BlobString AssetBundleVariant;
    public int Index;
}

public struct AssetBundleFullNameIndex
{
    public string AssetBundleName;
    public string AssetBundleVariant;
    public int Index;
}
