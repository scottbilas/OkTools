using Spreads.Buffers;
using UnityEngine;

// ReSharper disable BuiltInTypeReferenceStyle

namespace OkTools.Unity.AssetDb;

public static class LmdbValue
{
    public enum Type
    {
        Bool,
        SInt32,
        SInt32Pair,
        SInt64,
        String,
        StringArray,
        ImporterId,
        Hash128,
        MultilineString,
        AssetBundleNames,
        NamedPPtrArray,
        BlobPPtr,
    }

    public static unsafe void Dump(DumpContext dump, Type valueType, ref DirectBuffer value, string? valueName = null)
    {
        if (valueName == null)
            valueName = "Value";
        else
            dump.Csv?.Write($"{valueName},");

        switch (valueType)
        {
            case Type.Bool:
            {
                var b = value.Read<byte>() != 0;

                if (dump.Csv != null)
                    dump.Csv.Write(b ? "true" : "false");
                else
                    dump.Json!.WriteBoolean(valueName, b);
            }
            break;

            case Type.SInt32:
            {
                var i = value.Read<Int32>();

                if (dump.Csv != null)
                    dump.Csv.Write(i);
                else
                    dump.Json!.WriteNumber(valueName, i);
            }
            break;

            case Type.SInt32Pair:
            {
                var i0 = value.Read<Int32>();
                var i1 = value.Read<Int32>();

                if (dump.Csv != null)
                    dump.Csv.Write($"{i0},{i1}");
                else
                {
                    dump.Json!.WriteStartArray(valueName);
                    dump.Json.WriteNumberValue(i0);
                    dump.Json.WriteNumberValue(i1);
                    dump.Json.WriteEndArray();
                }
            }
            break;

            case Type.SInt64:
            {
                var i = value.Read<Int64>();

                if (dump.Csv != null)
                    dump.Csv.Write(i);
                else
                    dump.Json!.WriteNumber(valueName, i);
            }
            break;

            case Type.String:
            {
                var s = value.ReadAscii(true);

                if (dump.Csv != null)
                    dump.Csv.Write(s);
                else
                    dump.Json!.WriteString(valueName, s);
            }
            break;

            case Type.StringArray:
            {
                dump.Json?.WriteStartArray(valueName);

                var count = value.Read<UInt32>();
                for (var i = 0; i < count; ++i)
                {
                    var len = value.Read<UInt16>();
                    var str = value.ReadAscii(len, true);

                    if (dump.Csv != null)
                    {
                        if (i != 0)
                            dump.Csv.Write(',');
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

            case Type.MultilineString:
            {
                var m = value.ReadAscii(true).TrimEnd();

                if (dump.Csv != null)
                    dump.Csv.Write(m.Replace('\n', ','));
                else
                {
                    dump.Json!.WriteStartArray(valueName);
                    foreach (var s in m.Split('\n'))
                        dump.Json.WriteStringValue(s);
                    dump.Json.WriteEndArray();
                }
            }
            break;

            case Type.ImporterId:
            {
                var id = value.Read<ImporterId>();

                if (dump.Csv != null)
                    dump.Csv.Write($"{id.NativeImporterType},{id.ScriptedImporterType}");
                else if (dump.Config.OptTrim && id.NativeImporterType == -1 && !id.ScriptedImporterType.IsValid)
                    dump.Json!.WriteString(valueName, "(default)");
                else
                {
                    dump.Json!.WriteStartObject(valueName);
                        dump.Json.WriteNumber("NativeImporterType", id.NativeImporterType);
                        dump.Json.WriteString("ScriptedImporterType", id.ScriptedImporterType.ToString());
                    dump.Json.WriteEndObject();
                }
            }
            break;

            case Type.Hash128:
            {
                var hash = value.Read<Hash128>().ToString();

                if (dump.Csv != null)
                    dump.Csv.Write(hash);
                else
                    dump.Json!.WriteString(valueName, hash);
            }
            break;

            case Type.AssetBundleNames:
            {
                var names = value.Cast<BlobArray<AssetBundleFullNameIndexBlob>>();

                if (dump.Csv != null)
                    dump.Csv.Write($"AssetBundleFullNameIndex[{names->Length}]");
                else
                {
                    dump.Json!.WriteStartArray(valueName);
                    for (var i = 0; i < names->Length; ++i)
                    {
                        var blob = names->PtrAt(i);
                        dump.Json.WriteStartObject();
                        dump.Json.WriteString("AssetBundleName", blob->assetBundleName.GetString());
                        dump.Json.WriteString("AssetBundleVariant", blob->assetBundleVariant.GetString());
                        dump.Json.WriteNumber("Index", blob->index);
                        dump.Json.WriteEndObject();
                    }
                    dump.Json.WriteEndArray();
                }
            }
            break;

            case Type.NamedPPtrArray:
            {
                dump.Json?.WriteStartObject(valueName);

                var pairCount = value.Read<UInt32>() / 2;
                for (var i = 0; i < pairCount; ++i)
                {
                    var len = value.Read<UInt16>();
                    var str = value.ReadAscii(len, true);
                    len = value.Read<UInt16>();
                    if (len != sizeof(BlobPPtr))
                        throw new InvalidDataException("Invalid PPtr length");
                    var pptr = value.Read<BlobPPtr>();

                    if (dump.Csv != null)
                    {
                        if (i != 0)
                            dump.Csv.Write(',');
                        dump.Csv.Write(pptr.Equals(default)
                            ? $"{str}=null"
                            : $"{str}={pptr.guid};{pptr.localIdentifier};{pptr.type}");
                    }
                    else
                    {
                        dump.Json!.WriteStartObject(str);
                        if (pptr.type == FileIdentifierType.kInvalidType)
                            dump.Json.WriteNumber("instanceID", 0);
                        else
                        {
                            dump.Json.WriteNumber("fileID", pptr.localIdentifier);
                            dump.Json.WriteString("guid", pptr.guid.ToString());
                            dump.Json.WriteString("type", pptr.type.ToString());
                        }
                        dump.Json.WriteEndObject();
                    }
                }

                dump.Json?.WriteEndObject();
            }
            break;

            case Type.BlobPPtr:
            {
                var pptr = value.Read<BlobPPtr>();

                if (dump.Csv != null)
                {
                    dump.Csv.Write(pptr.Equals(default)
                        ? "null"
                        : $"{pptr.guid};{pptr.localIdentifier};{pptr.type}");
                }
                else
                {
                    dump.Json!.WriteStartObject(valueName);
                    if (pptr.type == FileIdentifierType.kInvalidType)
                        dump.Json.WriteNumber("instanceID", 0);
                    else
                    {
                        dump.Json.WriteNumber("fileID", pptr.localIdentifier);
                        dump.Json.WriteString("guid", pptr.guid.ToString());
                        dump.Json.WriteString("type", pptr.type.ToString());
                    }
                    dump.Json.WriteEndObject();
                }
            }
            break;

            default:
                throw new InvalidOperationException();
        }
    }
}

