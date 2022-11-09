using Spreads.Buffers;
using UnityEngine;

// ReSharper disable BuiltInTypeReferenceStyle

namespace OkTools.Unity.AssetDb;

public static class LmdbValue
{
    public enum Type
    {
        SInt32,
        SInt32Pair,
        SInt64,
        String,
        StringArray,
        ImporterId,
        Hash128,
        MultilineString,
        AssetBundleNames,
    }

    public static unsafe void Dump(DumpContext dump, Type valueType, ref DirectBuffer value, string? valueName = null)
    {
        if (valueName == null)
            valueName = "Value";
        else
            dump.Csv?.Write($"{valueName},");

        switch (valueType)
        {
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
                        var blob = names->RefElementFromBlob(i);
                        dump.Json.WriteStartObject();
                        dump.Json.WriteString("AssetBundleName", blob->AssetBundleName.ToString());
                        dump.Json.WriteString("AssetBundleVariant", blob->AssetBundleVariant.ToString());
                        dump.Json.WriteNumber("Index", blob->Index);
                        dump.Json.WriteEndObject();
                    }
                    dump.Json.WriteEndArray();
                }
            }
            break;

            default:
                throw new InvalidOperationException();
        }
    }
}

