using System.Text;
using System.Text.Json;
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

    public string Write(DirectBuffer value, StringBuilder sb)
    {
        string str;

        // ReSharper disable BuiltInTypeReferenceStyle
        switch (_propertyType)
        {
            case PropertyType.SInt32:
                str = value.Read<Int32>().ToString();
                break;

            case PropertyType.SInt32Pair:
                var i0 = value.Read<Int32>();
                var i1 = value.Read<Int32>();
                str = $"{i0},{i1}";
                break;

            case PropertyType.SInt64:
                str = value.Read<Int64>().ToString();
                break;

            case PropertyType.String:
                str = value.ReadAscii(true);
                break;

            case PropertyType.StringArray:
                var count = value.Read<UInt32>();
                for (var i = 0; i < count; ++i)
                {
                    if (sb.Length != 0)
                        sb.Append(',');

                    var len = value.Read<UInt16>();
                    value.ReadAscii(sb, len, true);
                }
                str = sb.ToString();
                sb.Clear();
                break;

            case PropertyType.ImporterId:
                var id = value.Read<ImporterId>();
                str = $"{id.NativeImporterType},{id.ScriptedImporterType}";
                break;

            case PropertyType.Hash128:
                str = value.Read<Hash128>().ToString();
                break;

            default:
                throw new InvalidOperationException();
        }
        // ReSharper restore BuiltInTypeReferenceStyle

        value.ExpectEnd();
        return str;
    }

    public void Write(DirectBuffer value, Utf8JsonWriter writer, string valueName)
    {
        // ReSharper disable BuiltInTypeReferenceStyle
        switch (_propertyType)
        {
            case PropertyType.SInt32:
                writer.WriteNumber(valueName, value.Read<Int32>());
                break;

            case PropertyType.SInt32Pair:
                writer.WriteStartArray(valueName);
                writer.WriteNumberValue(value.Read<Int32>());
                writer.WriteNumberValue(value.Read<Int32>());
                writer.WriteEndArray();
                break;

            case PropertyType.SInt64:
                writer.WriteNumber(valueName, value.Read<Int64>());
                break;

            case PropertyType.String:
                writer.WriteString(valueName, value.ReadAscii(true));
                break;

            case PropertyType.StringArray:
                writer.WriteStartArray(valueName);
                var count = value.Read<UInt32>();
                for (var i = 0; i < count; ++i)
                {
                    var len = value.Read<UInt16>();
                    var str = value.ReadAscii(len, true);
                    writer.WriteStringValue(str);
                }
                writer.WriteEndArray();
                break;

            case PropertyType.ImporterId:
                writer.WriteStartObject(valueName);
                var id = value.Read<ImporterId>();
                writer.WriteNumber("NativeImporterType", id.NativeImporterType);
                writer.WriteString("ScriptedImporterType", id.ScriptedImporterType.ToString());
                writer.WriteEndObject();
                break;

            case PropertyType.Hash128:
                writer.WriteString(valueName, value.Read<Hash128>().ToString());
                break;

            default:
                throw new InvalidOperationException();
        }
        // ReSharper restore BuiltInTypeReferenceStyle

        value.ExpectEnd();
    }
}
