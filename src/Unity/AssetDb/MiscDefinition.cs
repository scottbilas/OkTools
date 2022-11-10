using System.Text;
using Spreads.Buffers;

// ReSharper disable CommentTypo

namespace OkTools.Unity.AssetDb;

class MiscDefinition
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

    public static MiscDefinition Get(DirectBuffer name)
    {
        // TODO: binary search or hashtable or whatev (remember it's "starts with")
        foreach (var misc in k_all)
        {
            if (!name.Span.StartsWith(misc._nameBuffer))
                continue;

            return misc;
        }

        throw new InvalidDataException($"Unknown misc entry: {Encoding.ASCII.GetString(name)}");
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
