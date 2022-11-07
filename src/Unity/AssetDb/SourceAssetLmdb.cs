using System.Runtime.InteropServices;
using System.Text;
using Spreads.Buffers;
using Spreads.LMDB;
using UnityEngine;
using UnityEngine.AssetLmdb;

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
                var id = value.Read<ImporterID>();
                str = $"{id.nativeImporterType},{id.scriptedImporterType}";
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
[PublicAPI]
public class GuidPropertyIdToPropertyTable : LmdbTable
{
    public GuidPropertyIdToPropertyTable(LmdbDatabase db) : base(db, "GuidPropertyIDToProperty") {}

    public IEnumerable<(UnityGUID, PropertyDefinition, DirectBuffer)> SelectAll(ReadOnlyTransaction tx)
    {
        foreach (var (key, value) in Table.AsEnumerable(tx))
        {
            var found = PropertyDefinition.All.FirstOrDefault(propertyDef => key.Span.StartsWith(propertyDef.PrefixBuffer.Span));
            if (found == null)
                throw new InvalidDataException($"Unknown property: {Encoding.ASCII.GetString(key.Span)}");

            var unityGuid = key.Slice(found.PrefixBuffer.Length).ReadExpectEnd<UnityGUID>();
            yield return (unityGuid, found, value);
        }
    }
}

// GuidDB.cpp: GuidDB::m_pGuidToChildren; UnityGuid -> GuidChildren
[PublicAPI]
public class GuidToChildrenTable : LmdbTable
{
    public GuidToChildrenTable(LmdbDatabase db) : base(db, "GuidToChildren") {}

    public IEnumerable<(UnityGUID, GuidChildren)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ReadExpectEnd<UnityGUID>(),
            ReadChildren(kvp.Value)));

    static GuidChildren ReadChildren(DirectBuffer value)
    {
        var hash = value.Read<Hash128>();

        // count determined by using remaining space
        var remain = value.Length;
        var count = remain / UnityGUID.SizeOf;
        if (count * UnityGUID.SizeOf != remain)
            throw new InvalidOperationException("Size mismatch");

        var children = MemoryMarshal.Cast<byte, UnityGUID>(value.Span);

        // TODO: get rid of alloc (just point at lmdb memory), if this ever gets to the point where we need to care.
        // for now just do what's faster to write.
        return new GuidChildren { hash = hash, guids = children.ToArray() };
    }
}

// GuidDB.cpp: GuidDB::m_pGuidToIsDir; string -> uint8 (bool, 0 or nonzero)
[PublicAPI]
public class GuidToIsDirTable : LmdbTable
{
    public GuidToIsDirTable(LmdbDatabase db) : base(db, "GuidToIsDir") {}

    public IEnumerable<(UnityGUID, bool)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ReadExpectEnd<UnityGUID>(),
            kvp.Value.ReadExpectEnd<byte>() != 0));
}

// GuidDB.cpp: GuidDB::m_pGuidToPath; UnityGuid -> string path
[PublicAPI]
public class GuidToPathTable : LmdbTable
{
    public GuidToPathTable(LmdbDatabase db) : base(db, "GuidToPath") {}

    public IEnumerable<(UnityGUID, string)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ReadExpectEnd<UnityGUID>(),
            kvp.Value.ToAsciiString()));
}

// HashDB.cpp: HashDB::m_pPathToHash; string path -> HashDBValue
[PublicAPI]
public class PathToHashTable : LmdbTable
{
    public PathToHashTable(LmdbDatabase db) : base(db, "hash") {}

    public IEnumerable<(string, HashDBValue)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ToAsciiString(),
            kvp.Value.ReadExpectEnd<HashDBValue>()));
}

// SourceAssetDB.cpp: SourceAssetDB::m_Misc, with keys from SourceAssetDB.cpp "s_Misc_*" area
/*
class MiscTable : LmdbTable
{

}

string key -> key-dependent value

    assetBundleNames -> BlobArray<AssetBundleFullNameIndex>* (needs parsing to interpret, see SourceAssetDBWriteTxn::AddAssetBundleNames)

        struct AssetBundleFullNameIndex
        {
            BlobString assetBundleName;
            BlobString assetBundleVariant;
            int index;
        };

        // Find existing asset bundles
        blobRootsSize = blobRoots->size();
        for (UInt32 i = 0; i < blobRootsSize; i++)
        {
            const AssetBundleFullNameIndex& abi = (*blobRoots)[i];
            core::string fullName = BuildAssetBundleFullName(abi.assetBundleName.c_str(), abi.assetBundleVariant.c_str());
            auto iter = assetBundleFullNames.find(fullName);
            if (iter != assetBundleFullNames.end())
                assetBundleIndices[iter->second] = core::pair<SInt32, bool>(abi.index, false);
        }

        struct BlobString
        {
            BlobOffsetPtr<char> m_Data;
            //...
        };

        template<typename TYPE>
        struct BlobOffsetPtr : BlobOffsetPtrBase
        {
            SInt32 m_Offset;

            const_ptr_type GetUnsafe() const
            {
                return reinterpret_cast<ptr_type>(reinterpret_cast<std::size_t>(this) + m_Offset);
            }
        };

    refreshVersion -> int version (default -1)
    shaderCacheClearVersion -> int version (default -1)
    crashedImportPaths -> string[] (default empty)
*/

// GuidDB.cpp: GuidDB::m_pPathToGuid; string path -> GuidDBValue
[PublicAPI]
public class PathToGuidTable : LmdbTable
{
    public PathToGuidTable(LmdbDatabase db) : base(db, "PathToGuid") {}

    public IEnumerable<(string, GuidDBValue)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ToAsciiString(),
            kvp.Value.ReadExpectEnd<GuidDBValue>()));
}

// SourceAssetDB.cpp: SourceAssetDB::m_PropertyIDToType; string property -> string type
[PublicAPI]
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

    public PropertyIdToTypeTable(LmdbDatabase db) : base(db, "PropertyIDToType") {}

    public IEnumerable<(string, string)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            kvp.Key.ToAsciiString(),
            kvp.Value.ToAsciiString()));
}

// SourceAssetDB.cpp: SourceAssetDB::m_RootFolders
[PublicAPI]
public class RootFoldersTable : LmdbTable
{
    public RootFoldersTable(LmdbDatabase db) : base(db, "RootFolders") {}

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

// SourceAssetDB.h: RootFolderPropertiesBlob
[StructLayout(LayoutKind.Sequential)]
struct RootFolderPropertiesBlob
{
    public UnityGUID Guid;
    public bool Immutable;
    public BlobString MountPoint;
    public BlobString Folder;
    public BlobString PhysicalPath;
}

[StructLayout(LayoutKind.Sequential)]
struct BlobString
{
    int _offset; // start of string characters as an offset from "this"

    public unsafe string GetStringFromBlob()
    {
        fixed (BlobString* self = &this)
        {
            var start = (byte*)self + _offset;

            var end = start;
            while (*end != 0)
                ++end;

            return Encoding.ASCII.GetString(start, (int)(end - start));
        }
    }
}

// TODO: get rid of this alloc-by-default thing
public struct RootFolderProperties
{
    public UnityGUID Guid;
    public bool Immutable;
    public string MountPoint;
    public string Folder;
    public string PhysicalPath;
}
