using System.Runtime.InteropServices;
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

// GuidDB.cpp: GuidDB::m_GuidPropertyIDToProperty
[PublicAPI]
public class GuidPropertyIdToPropertyTable : LmdbTable
{
    public GuidPropertyIdToPropertyTable(LmdbDatabase db) : base(db, "GuidPropertyIDToProperty") {}


}

/* TODO: GuidPropertyIDToProperty

unityguid -(many)> property

typedef PropertyDefinition<SInt64> SInt64PropertyDefinition;
typedef PropertyDefinition<SInt32> SInt32PropertyDefinition;
typedef PropertyDefinition<Hash128> Hash128PropertyDefinition;
typedef PropertyDefinition<core::string> StringPropertyDefinition;
typedef PropertyDefinition<core::string_ref> StringRefPropertyDefinition;
typedef PropertyDefinition<core::pair<SInt32, SInt32> > SInt32PairPropertyDefinition;
typedef PropertyDefinition<dynamic_array<core::string> > StringArrayPropertyDefinition;
typedef PropertyDefinition<dynamic_array<core::string_ref> > StringRefArrayPropertyDefinition;
typedef PropertyDefinition<AssetDatabase::ImporterID> ImporterIDPropertyDefinition;

EXPORT_COREMODULE const StringArrayPropertyDefinition kLabelsPropDef("labels", true);
EXPORT_COREMODULE const SInt32PropertyDefinition kAssetBundleIndexPropDef("AssetBundleIndex", false);
EXPORT_COREMODULE const StringPropertyDefinition kAssetBundleNamePropDef("assetBundleName", true);
EXPORT_COREMODULE const StringPropertyDefinition kAssetBundleVariantPropDef("assetBundleVariant", true);
EXPORT_COREMODULE const SInt64PropertyDefinition kMainObjectLocalIdentifierInFilePropDef("MainObjectLocalIdentifierInFile", false);
EXPORT_COREMODULE const ImporterIDPropertyDefinition kImporterOverridePropDef("importerOverride", true);
EXPORT_COREMODULE const Hash128PropertyDefinition kImportLogFilePropDef("importLogFile", false);
EXPORT_COREMODULE const SInt32PairPropertyDefinition kImportLogEntriesCountPropDef("ImportLogEntriesCount", false);
EXPORT_COREMODULE const StringPropertyDefinition kScriptCompilationAssetPathPropDef("scriptCompilationAssetPath", false);
*/

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
