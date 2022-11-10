using System.Runtime.CompilerServices;
using System.Text;
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

        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef0",            false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef1",            false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef2",            false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef3",            false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef4",            false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef5",            false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef6",            false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef7",            false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef8",            false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef9",            false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef10",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef11",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef12",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef13",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef14",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef15",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef16",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef17",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef18",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef19",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef20",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef21",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef22",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef23",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef24",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef25",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef26",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef27",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef28",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef29",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef30",           false, LmdbValue.Type.Hash128),
        /*Hash128PropertyDefinition*/                                        new("ArtifactFilePropDef31",           false, LmdbValue.Type.Hash128),

    };
}
