// ReSharper disable BuiltInTypeReferenceStyle
// ReSharper disable InconsistentNaming

namespace UnityEngine.AssetLmdb;

#pragma warning disable CA1720

public struct GuidDBValue // Modules/AssetDatabase/Editor/V2/GuidDB.h
{
    public UnityGuid guid;
    public Hash128 metaFileHash;    // these are both SpookyV2
    public Hash128 assetFileHash;

    /* manual scanner

    using var metaFile = File.OpenRead(testAssetMetaPath);
    var deserialized = (dynamic)new SharpYaml.Serialization.Serializer().Deserialize(metaFile);
    var hasher = SpookyHashV2Factory.Instance.Create();

    var guid = ((string)deserialized["guid"]).ToUpper();
    var metaFileHash = hasher.ComputeHash(File.ReadAllBytes(testAssetMetaPath));
    var assetFileHash = hasher.ComputeHash(File.ReadAllBytes(testAssetPath));
    */
}

public struct HashDBValue // Modules/AssetDatabase/Editor/V2/HashDB.h
{
    public Hash128 hash;
    public long time; // C++ DateTime not binary compatible because extra field in C# version, but easy to convert (they both use the same ticks epoch+resolution)
    public UInt64 fileSize;
    public bool isUntrusted;
}

public struct GuidChildren
{
    public Hash128 hash;
    public UnityGuid[] guids;
}
