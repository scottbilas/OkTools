using System.Runtime.InteropServices;
using System.Text;
using Spreads.Buffers;
using Spreads.LMDB;
using UnityEngine;
using UnityEngine.AssetLmdb;

namespace OkTools.Unity;

public class SourceAssetLmdb : AssetLmdb
{
    const uint k_expectedDbVersion = 9;

    public SourceAssetLmdb(NPath projectRoot)
        : base(projectRoot.Combine(UnityProjectConstants.SourceAssetDbNPath), k_expectedDbVersion) {}
}

public class PropertyDefinition
{
    public readonly string Prefix;
    public readonly DirectBuffer PrefixBuffer;
    public readonly bool IsInMetaFile;
    public readonly Func<DirectBuffer, StringBuilder, string> ToCsv;

    PropertyDefinition(string prefix, bool isInMetaFile, Func<DirectBuffer, StringBuilder, string> toCsv)
    {
        Prefix = prefix;
        PrefixBuffer = LmdbUtils.StringToBuffer(prefix, false);
        IsInMetaFile = isInMetaFile;
        ToCsv = toCsv;
    }

    PropertyDefinition(string prefix, bool isInMetaFile, Func<DirectBuffer, string> toCsv)
        : this(prefix, isInMetaFile, (buffer, _) => toCsv(buffer)) {}

    public static readonly PropertyDefinition[] All =
    {
        // from top of PropertyDefinition.cpp
        /*StringArrayPropertyDefinition kLabelsPropDef*/                     new("labels",                          true,  FromStringArray),
        /*SInt32PropertyDefinition kAssetBundleIndexPropDef*/                new("AssetBundleIndex",                false, FromSInt32),
        /*StringPropertyDefinition kAssetBundleNamePropDef*/                 new("assetBundleName",                 true,  FromString),
        /*StringPropertyDefinition kAssetBundleVariantPropDef*/              new("assetBundleVariant",              true,  FromString),
        /*SInt64PropertyDefinition kMainObjectLocalIdentifierInFilePropDef*/ new("MainObjectLocalIdentifierInFile", false, FromSInt64),
        /*ImporterIDPropertyDefinition kImporterOverridePropDef*/            new("importerOverride",                true,  FromImporterId),
        /*Hash128PropertyDefinition kImportLogFilePropDef*/                  new("importLogFile",                   false, FromHash128),
        /*SInt32PairPropertyDefinition kImportLogEntriesCountPropDef*/       new("ImportLogEntriesCount",           false, FromSInt32Pair),
        /*StringPropertyDefinition kScriptCompilationAssetPathPropDef*/      new("scriptCompilationAssetPath",      false, FromString),
        /*Hash128PropertyDefinition kImporterErrorFilePropDef*/              new("importerErrorFile",               false, FromHash128),
        /*SInt32PropertyDefinition kAssetOriginProductIdPropDef*/            new("productId",                       true,  FromSInt32),
        /*StringPropertyDefinition kAssetOriginPackageNamePropDef*/          new("packageName",                     true,  FromString),
        /*StringPropertyDefinition kAssetOriginPackageVersionPropDef*/       new("packageVersion",                  true,  FromString),
        /*StringPropertyDefinition kAssetOriginAssetPathPropDef*/            new("assetPath",                       true,  FromString),
        /*SInt32PropertyDefinition kAssetOriginUploadIdPropDef*/             new("uploadId",                        true,  FromSInt32),
    };

    static string FromStringArray(DirectBuffer value, StringBuilder sb)
    {
        if (value.IsEmpty)
            return "";

        var offset = 0;

        var count = value.ReadUInt32(offset);
        if (count == 0)
            return "";
        offset += sizeof(UInt32);

        for (var i = 0; i < count; ++i)
        {
            var len = value.ReadUInt16(offset);
            offset += sizeof(UInt16);

            if (sb.Length != 0)
                sb.Append(',');
            sb.Append(Encoding.ASCII.GetString(value.Span[offset..(offset+len-1)]));
            offset += len;
        }

        var str = sb.ToString();
        sb.Clear();
        return str;
    }

    static string FromString(DirectBuffer value)
    {
        return Encoding.ASCII.GetString(value.Span[..^1]);
    }

    static string FromSInt32(DirectBuffer value)
    {
        var i = value.Read<Int32>(0);
        return i.ToString();
    }

    static string FromSInt32Pair(DirectBuffer value)
    {
        var i0 = value.Read<Int32>(0);
        var i1 = value.Read<Int32>(4);
        return $"{i0},{i1}";
    }

    static string FromSInt64(DirectBuffer value)
    {
        var i = value.Read<Int64>(0);
        return i.ToString();
    }

    static string FromImporterId(DirectBuffer value)
    {
        var id = value.Read<ImporterID>(0);
        return $"{id.nativeImporterType},{id.scriptedImporterType}";
    }

    static string FromHash128(DirectBuffer value)
    {
        return value.Read<Hash128>(0).ToString();
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

            var unityGuid = key.Read<UnityGUID>(found.PrefixBuffer.Length);
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
            MemoryMarshal.Read<UnityGUID>(kvp.Key.Span),
            ReadChildren(kvp.Value)));

    static GuidChildren ReadChildren(DirectBuffer value)
    {
        var hash = value.Read<Hash128>(0);

        // count determined by using remaining space
        var remain = value.Length - Hash128.SizeOf;
        var count = remain / UnityGUID.SizeOf;
        if (count * UnityGUID.SizeOf != remain)
            throw new InvalidOperationException("Size mismatch");

        var children = MemoryMarshal.Cast<byte, UnityGUID>(value.Span[Hash128.SizeOf..]);

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
            MemoryMarshal.Read<UnityGUID>(kvp.Key.Span),
            kvp.Value.ReadByte(0) != 0));
}

// GuidDB.cpp: GuidDB::m_pGuidToPath; UnityGuid -> string path
[PublicAPI]
public class GuidToPathTable : LmdbTable
{
    public GuidToPathTable(LmdbDatabase db) : base(db, "GuidToPath") {}

    public IEnumerable<(UnityGUID, string)> SelectAll(ReadOnlyTransaction tx) =>
        Table.AsEnumerable(tx).Select(kvp => (
            MemoryMarshal.Read<UnityGUID>(kvp.Key.Span),
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
            MemoryMarshal.Read<HashDBValue>(kvp.Value.Span)));
}

/* TODO: Misc
SourceAssetDB.cpp: SourceAssetDB::m_Misc, with keys from SourceAssetDB.cpp "s_Misc_*" area

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
            MemoryMarshal.Read<GuidDBValue>(kvp.Value.Span)));
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

/* TODO: RootFolders
SourceAssetDB.cpp: SourceAssetDB::m_RootFolders

string -> RootFolderPropertiesBlog

    // SourceAssetDB.h
    struct RootFolderPropertiesBlob
    {
        UnityGUID Guid;
        bool Immutable;
        BlobString MountPoint;
        BlobString Folder;
        BlobString PhysicalPath;
    };
*/
