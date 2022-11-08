using Spreads.Buffers;
using UnityEngine;

#pragma warning disable CS0649

namespace OkTools.Unity.AssetDb;

public class PropertyDefinition
{
    readonly PropertyType _propertyType;
    readonly byte[] _nameBuffer;

    public readonly string Name;
    public readonly bool IsInMetaFile;

    PropertyDefinition(string name, bool isInMetaFile, PropertyType type)
    {
        _propertyType = type;
        _nameBuffer = LmdbUtils.StringToBytes(name, false);

        Name = name;
        IsInMetaFile = isInMetaFile;
    }

    static readonly PropertyDefinition[] k_all =
    {
        // from top of PropertyDefinition.cpp
        /*StringArrayPropertyDefinition kLabelsPropDef*/                     new("labels",                          true,  PropertyType.StringArray),
        /*SInt32PropertyDefinition kAssetBundleIndexPropDef*/                new("AssetBundleIndex",                false, PropertyType.SInt32),
        /*StringPropertyDefinition kAssetBundleNamePropDef*/                 new("assetBundleName",                 true,  PropertyType.String),
        /*StringPropertyDefinition kAssetBundleVariantPropDef*/              new("assetBundleVariant",              true,  PropertyType.String),
        /*SInt64PropertyDefinition kMainObjectLocalIdentifierInFilePropDef*/ new("MainObjectLocalIdentifierInFile", false, PropertyType.SInt64),
        /*ImporterIDPropertyDefinition kImporterOverridePropDef*/            new("importerOverride",                true,  PropertyType.ImporterId),
        /*Hash128PropertyDefinition kImportLogFilePropDef*/                  new("importLogFile",                   false, PropertyType.Hash128),
        /*SInt32PairPropertyDefinition kImportLogEntriesCountPropDef*/       new("ImportLogEntriesCount",           false, PropertyType.SInt32Pair),
        /*StringPropertyDefinition kScriptCompilationAssetPathPropDef*/      new("scriptCompilationAssetPath",      false, PropertyType.String),
        /*Hash128PropertyDefinition kImporterErrorFilePropDef*/              new("importerErrorFile",               false, PropertyType.Hash128),
        /*SInt32PropertyDefinition kAssetOriginProductIdPropDef*/            new("productId",                       true,  PropertyType.SInt32),
        /*StringPropertyDefinition kAssetOriginPackageNamePropDef*/          new("packageName",                     true,  PropertyType.String),
        /*StringPropertyDefinition kAssetOriginPackageVersionPropDef*/       new("packageVersion",                  true,  PropertyType.String),
        /*StringPropertyDefinition kAssetOriginAssetPathPropDef*/            new("assetPath",                       true,  PropertyType.String),
        /*SInt32PropertyDefinition kAssetOriginUploadIdPropDef*/             new("uploadId",                        true,  PropertyType.SInt32),
    };

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

    enum PropertyType
    {
        SInt32,
        SInt32Pair,
        SInt64,
        String,
        StringArray,
        ImporterId,
        Hash128,
    }

    public void Write(DumpContext dump, ref DirectBuffer value)
    {
        // ReSharper disable BuiltInTypeReferenceStyle
        switch (_propertyType)
        {
            case PropertyType.SInt32:
            {
                var i = value.Read<Int32>();

                if (dump.Csv != null)
                    dump.Csv?.Write(i);
                else
                    dump.Json!.WriteNumber("Value", i);
            }
            break;

            case PropertyType.SInt32Pair:
            {
                var i0 = value.Read<Int32>();
                var i1 = value.Read<Int32>();

                if (dump.Csv != null)
                    dump.Csv.Write($"{i0},{i1}");
                else
                {
                    dump.Json!.WriteStartArray("Value");
                    dump.Json.WriteNumberValue(i0);
                    dump.Json.WriteNumberValue(i1);
                    dump.Json.WriteEndArray();
                }
            }
            break;

            case PropertyType.SInt64:
            {
                var i = value.Read<Int64>();

                if (dump.Csv != null)
                    dump.Csv.Write(i);
                else
                    dump.Json!.WriteNumber("Value", i);
            }
            break;

            case PropertyType.String:
            {
                var s = value.ReadAscii(true);

                if (dump.Csv != null)
                    dump.Csv.Write(s);
                else
                    dump.Json!.WriteString("Value", s);
            }
            break;

            case PropertyType.StringArray:
            {
                dump.Json?.WriteStartArray("Value");

                var count = value.Read<UInt32>();
                for (var i = 0; i < count; ++i)
                {
                    var len = value.Read<UInt16>();
                    var str = value.ReadAscii(len, true);

                    if (dump.Csv != null)
                    {
                        if (i != 0)
                            dump.Buffer.Append(',');
                        dump.Csv.Write(str);
                    }
                    else
                    {
                        dump.Json!.WriteStringValue(str);
                    }
                }

                dump.Json?.WriteEndArray();
            }
            break;

            case PropertyType.ImporterId:
            {
                var id = value.Read<ImporterId>();

                if (dump.Csv != null)
                    dump.Csv.Write($"{id.NativeImporterType},{id.ScriptedImporterType}");
                else
                {
                    dump.Json!.WriteStartObject("Value");
                    dump.Json.WriteNumber("NativeImporterType", id.NativeImporterType);
                    dump.Json.WriteString("ScriptedImporterType", id.ScriptedImporterType.ToString());
                    dump.Json.WriteEndObject();
                }
            }
            break;

            case PropertyType.Hash128:
            {
                var hash = value.Read<Hash128>().ToString();

                if (dump.Csv != null)
                    dump.Csv?.Write(hash);
                else
                    dump.Json!.WriteString("Value", hash);
            }
            break;

            default:
                throw new InvalidOperationException();
        }
        // ReSharper restore BuiltInTypeReferenceStyle
    }
}
