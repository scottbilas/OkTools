using Spreads.Buffers;
using UnityEngine;

// ReSharper disable CommentTypo
// ReSharper disable BuiltInTypeReferenceStyle

// TODO: better way to do the blob thing
// TODO: get rid of required allocs (strings and arrays) and let it be deferred until needed, so long as transaction is open. maybe a "handle" that has the tx and a void* to base of key/value results..

#pragma warning disable CS0649

namespace OkTools.Unity.AssetDb;

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

public class MiscDefinition
{
    readonly MiscType _miscType;

    public readonly string Name;
    public readonly byte[] NameBuffer;

    MiscDefinition(string name, MiscType type)
    {
        Name = name;
        NameBuffer = LmdbUtils.StringToBytes(name, false);

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
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
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

