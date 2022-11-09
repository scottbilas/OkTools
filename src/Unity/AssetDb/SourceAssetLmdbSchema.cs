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

struct AssetBundleFullNameIndexBlob
{
    public BlobString AssetBundleName;
    public BlobString AssetBundleVariant;
    public int Index;
}

struct GuidDbValue // Modules/AssetDatabase/Editor/V2/GuidDB.h
{
    public UnityGuid Guid;
    public Hash128 MetaFileHash;    // these are both SpookyV2
    public Hash128 AssetFileHash;

    /* manual scanner

    using var metaFile = File.OpenRead(testAssetMetaPath);
    var deserialized = (dynamic)new SharpYaml.Serialization.Serializer().Deserialize(metaFile);
    var hasher = SpookyHashV2Factory.Instance.Create();

    var guid = ((string)deserialized["guid"]).ToUpper();
    var metaFileHash = hasher.ComputeHash(File.ReadAllBytes(testAssetMetaPath));
    var assetFileHash = hasher.ComputeHash(File.ReadAllBytes(testAssetPath));
    */
}

struct HashDbValue // Modules/AssetDatabase/Editor/V2/HashDB.h
{
    public Hash128 Hash;
    public long Time;
    public UInt64 FileSize;
    public bool IsUntrusted;

    public DateTime TimeAsDateTime => new(Time); // C++ DateTime not binary compatible because extra field in C# version, but easy to convert (they both use the same ticks epoch+resolution)
}

public class MiscDefinition
{
    readonly byte[] _nameBuffer;

    public readonly string Name;
    public readonly LmdbValue.Type ValueType;

    MiscDefinition(string name, LmdbValue.Type type)
    {
        _nameBuffer = LmdbUtils.StringToBytes(name, false);

        Name = name;
        ValueType = type;
    }

    public static MiscDefinition? Find(ref DirectBuffer name)
    {
        // TODO: binary search or hashtable or whatev (remember it's "starts with")
        foreach (var misc in k_all)
        {
            if (name.Span.StartsWith(misc._nameBuffer))
            {
                name = name.Slice(misc._nameBuffer.Length);
                return misc;
            }
        }

        return null;
    }

    static readonly MiscDefinition[] k_all =
    {
        // look for s_Misc_* in SourceAssetDB.cpp
        /*s_Misc_RefreshVersion*/          new("refreshVersion",          LmdbValue.Type.SInt32),           // refreshVersion -> int version (default -1 if missing)
        /*s_Misc_ShaderCacheClearVersion*/ new("shaderCacheClearVersion", LmdbValue.Type.SInt32),           // shaderCacheClearVersion -> int version (default -1 if missing)
        /*s_Misc_CrashedImportPaths*/      new("crashedImportPaths",      LmdbValue.Type.MultilineString),  // crashedImportPaths -> string[] (default empty if missing)
        /*s_AssetBundNames*/               new("assetBundleNames",        LmdbValue.Type.AssetBundleNames), // assetBundleNames -> BlobArray<AssetBundleFullNameIndex>* (needs parsing to interpret, see SourceAssetDBWriteTxn::AddAssetBundleNames)
    };
}
