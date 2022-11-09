using Spreads.Buffers;

#pragma warning disable CS0649

namespace OkTools.Unity.AssetDb;

public class PropertyDefinition
{
    readonly byte[] _nameBuffer;

    public readonly string Name;
    public readonly bool IsInMetaFile;
    public readonly LmdbValue.Type ValueType;

    PropertyDefinition(string name, bool isInMetaFile, LmdbValue.Type type)
    {
        _nameBuffer = LmdbUtils.StringToBytes(name, false);

        Name = name;
        IsInMetaFile = isInMetaFile;
        ValueType = type;
    }

    public static PropertyDefinition? Find(ref DirectBuffer name)
    {
        // TODO: binary search or hashtable or whatev (remember it's "starts with")
        foreach (var property in k_all)
        {
            if (name.Span.StartsWith(property._nameBuffer))
            {
                name = name.Slice(property._nameBuffer.Length);
                return property;
            }
        }

        return null;
    }

    static readonly PropertyDefinition[] k_all =
    {
        // from top of PropertyDefinition.cpp
        /*StringArrayPropertyDefinition kLabelsPropDef*/                     new("labels",                          true,  LmdbValue.Type.StringArray),
        /*SInt32PropertyDefinition kAssetBundleIndexPropDef*/                new("AssetBundleIndex",                false, LmdbValue.Type.SInt32),
        /*StringPropertyDefinition kAssetBundleNamePropDef*/                 new("assetBundleName",                 true,  LmdbValue.Type.String),
        /*StringPropertyDefinition kAssetBundleVariantPropDef*/              new("assetBundleVariant",              true,  LmdbValue.Type.String),
        /*SInt64PropertyDefinition kMainObjectLocalIdentifierInFilePropDef*/ new("MainObjectLocalIdentifierInFile", false, LmdbValue.Type.SInt64),
        /*ImporterIDPropertyDefinition kImporterOverridePropDef*/            new("importerOverride",                true,  LmdbValue.Type.ImporterId),
        /*Hash128PropertyDefinition kImportLogFilePropDef*/                  new("importLogFile",                   false, LmdbValue.Type.Hash128),
        /*SInt32PairPropertyDefinition kImportLogEntriesCountPropDef*/       new("ImportLogEntriesCount",           false, LmdbValue.Type.SInt32Pair),
        /*StringPropertyDefinition kScriptCompilationAssetPathPropDef*/      new("scriptCompilationAssetPath",      false, LmdbValue.Type.String),
        /*Hash128PropertyDefinition kImporterErrorFilePropDef*/              new("importerErrorFile",               false, LmdbValue.Type.Hash128),
        /*SInt32PropertyDefinition kAssetOriginProductIdPropDef*/            new("productId",                       true,  LmdbValue.Type.SInt32),
        /*StringPropertyDefinition kAssetOriginPackageNamePropDef*/          new("packageName",                     true,  LmdbValue.Type.String),
        /*StringPropertyDefinition kAssetOriginPackageVersionPropDef*/       new("packageVersion",                  true,  LmdbValue.Type.String),
        /*StringPropertyDefinition kAssetOriginAssetPathPropDef*/            new("assetPath",                       true,  LmdbValue.Type.String),
        /*SInt32PropertyDefinition kAssetOriginUploadIdPropDef*/             new("uploadId",                        true,  LmdbValue.Type.SInt32),
    };
}
