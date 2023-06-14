using System.Runtime.CompilerServices;
using System.Text;
using Spreads.Buffers;

#pragma warning disable CS0649

namespace OkTools.Unity.AssetDb;

public class PropertyDefinition
{
    readonly byte[] _nameBuffer;

    public readonly string Name;
    public readonly LmdbValue.Type ValueType;

    PropertyDefinition(string name, LmdbValue.Type type)
    {
        _nameBuffer = LmdbUtils.StringToBytes(name, false);

        Name = name;
        ValueType = type;
    }

    public static (PropertyDefinition, T) Get<T>(DirectBuffer name) where T : unmanaged
    {
        // TODO: binary search or hashtable or whatev (remember it's "starts with")
        foreach (var property in k_all)
        {
            if (!name.Span.StartsWith(property._nameBuffer))
                continue;
            if (property._nameBuffer.Length + Unsafe.SizeOf<T>() != name.Length)
                continue;

            name = name.Slice(property._nameBuffer.Length);
            var second = name.ReadExpectEnd<T>();
            return (property, second);
        }

        throw new InvalidDataException($"Unknown property: {Encoding.ASCII.GetString(name)}");
    }

    static readonly PropertyDefinition[] k_all = new PropertyDefinition[]
    {
        // from PropertyDefinition.cpp
        /*StringArrayPropertyDefinition kLabelsPropDef*/                         new("labels",                          LmdbValue.Type.StringArray),
        /*SInt32PropertyDefinition kAssetBundleIndexPropDef*/                    new("AssetBundleIndex",                LmdbValue.Type.SInt32),
        /*StringPropertyDefinition kAssetBundleNamePropDef*/                     new("assetBundleName",                 LmdbValue.Type.String),
        /*StringPropertyDefinition kAssetBundleVariantPropDef*/                  new("assetBundleVariant",              LmdbValue.Type.String),
        /*SInt64PropertyDefinition kMainObjectLocalIdentifierInFilePropDef*/     new("MainObjectLocalIdentifierInFile", LmdbValue.Type.SInt64),
        /*ImporterIDPropertyDefinition kImporterOverridePropDef*/                new("importerOverride",                LmdbValue.Type.ImporterId),
        /*Hash128PropertyDefinition kImportLogFilePropDef*/                      new("importLogFile",                   LmdbValue.Type.Hash128),
        /*SInt32PairPropertyDefinition kImportLogEntriesCountPropDef*/           new("ImportLogEntriesCount",           LmdbValue.Type.SInt32Pair),
        /*StringPropertyDefinition kScriptCompilationAssetPathPropDef*/          new("scriptCompilationAssetPath",      LmdbValue.Type.String),
        /*Hash128PropertyDefinition kImporterErrorFilePropDef*/                  new("importerErrorFile",               LmdbValue.Type.Hash128),

        /*SInt32PropertyDefinition kAssetOriginProductIdPropDef*/                new("productId",                       LmdbValue.Type.SInt32),
        /*StringPropertyDefinition kAssetOriginPackageNamePropDef*/              new("packageName",                     LmdbValue.Type.String),
        /*StringPropertyDefinition kAssetOriginPackageVersionPropDef*/           new("packageVersion",                  LmdbValue.Type.String),
        /*StringPropertyDefinition kAssetOriginAssetPathPropDef*/                new("assetPath",                       LmdbValue.Type.String),
        /*SInt32PropertyDefinition kAssetOriginUploadIdPropDef*/                 new("uploadId",                        LmdbValue.Type.SInt32),

        // pending in assetpipeline/no-script-import
        /*BoolPropertyDefinition kAssetRequireImportFlagPropDef*/                new("assetReqImport",                  LmdbValue.Type.Bool),
        /*BoolPropertyDefinition kScriptRequireImportFlagPropDef*/               new("scriptReqImport",                 LmdbValue.Type.Bool),
        /*BoolPropertyDefinition kManagedPluginFlagPropDef*/                     new("managedPlugin",                   LmdbValue.Type.Bool),
        /*StringRefPropertyDefinition kNativePluginPathPropDef*/                 new("nativePluginPath",                LmdbValue.Type.String),
        /*NamedPPtrArrayPropertyDefinition kMonoScriptDefaultReferencesPropDef*/ new("MonoImporter.defaultReferences",  LmdbValue.Type.NamedPPtrArray),
        /*BlobPPtrPropertyDefinition kMonoScriptIconPropDef*/                    new("MonoImporter.icon",               LmdbValue.Type.BlobPPtr),
        /*SInt32PropertyDefinition kMonoScriptExecutionOrderPropDef*/            new("MonoImporter.executionOrder",     LmdbValue.Type.SInt32)
    }
    .Concat(Enumerable
        .Range(0, 32)
        .Select(i => /*Hash128PropertyDefinition*/ new PropertyDefinition("ArtifactFilePropDef" + i, LmdbValue.Type.Hash128)))
    .ToArray();
}
