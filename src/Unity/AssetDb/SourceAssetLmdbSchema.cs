using UnityEngine;

// ReSharper disable InconsistentNaming
// ReSharper disable CommentTypo
// ReSharper disable BuiltInTypeReferenceStyle

// TODO: better way to do the blob thing
// TODO: get rid of required allocs (strings and arrays) and let it be deferred until needed, so long as transaction is open. maybe a "handle" that has the tx and a void* to base of key/value results..

#pragma warning disable CS0649

namespace OkTools.Unity.AssetDb;

// SourceAssetDB.h: RootFolderPropertiesBlob
struct RootFolderPropertiesBlob
{
    public UnityGUID  Guid;
    public bool       Immutable;
    public BlobString MountPoint;
    public BlobString Folder;
    public BlobString PhysicalPath;
}

struct AssetBundleFullNameIndexBlob
{
    public BlobString assetBundleName;
    public BlobString assetBundleVariant;
    public int        index;
}

struct GuidDBValue // Modules/AssetDatabase/Editor/V2/GuidDB.h
{
    public UnityGUID guid;
    public Hash128   metaFileHash;  // these are both SpookyV2
    public Hash128   assetFileHash;

    /* manual scanner

    using var metaFile = File.OpenRead(testAssetMetaPath);
    var deserialized = (dynamic)new SharpYaml.Serialization.Serializer().Deserialize(metaFile);
    var hasher = SpookyHashV2Factory.Instance.Create();

    var guid = ((string)deserialized["guid"]).ToUpper();
    var metaFileHash = hasher.ComputeHash(File.ReadAllBytes(testAssetMetaPath));
    var assetFileHash = hasher.ComputeHash(File.ReadAllBytes(testAssetPath));
    */
}

struct HashDBValue // Modules/AssetDatabase/Editor/V2/HashDB.h
{
    public Hash128 hash;
    public long    time;
    public UInt64  fileSize;
    public bool    isUntrusted;

    public DateTime TimeAsDateTime => new(time); // C++ DateTime not binary compatible because extra field in C# version, but easy to convert (they both use the same ticks epoch+resolution)
}
