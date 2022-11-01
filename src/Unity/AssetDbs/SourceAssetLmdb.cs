using System.Runtime.InteropServices;
using Spreads.LMDB;
using UnityEngine;
using UnityEngine.AssetLmdb;

namespace OkTools.Unity;

public class SourceAssetLmdb : AssetLmdb
{
    const uint k_expectedDbVersion = 6;

    public SourceAssetLmdb(NPath projectRoot)
        : base(projectRoot.Combine(UnityProjectConstants.SourceAssetDbNPath), k_expectedDbVersion) {}
}

/* TODO: GuidPropertyIDToProperty
GuidDB.cpp: GuidDB::m_GuidPropertyIDToProperty

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

// TODO: GuidToChildren
// GuidDB.cpp: GuidDB::m_pGuidToChildren
//
// unityguid -> struct { Hash128, UnityGUID[] } // for count, just divide it out from value size (minus hash128)

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

// TODO: PropertyIDToType
// SourceAssetDB.cpp: SourceAssetDB::m_PropertyIDToType
//
// string -> string

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
