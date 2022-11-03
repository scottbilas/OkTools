using System.Text;
using DocoptNet;
using NiceIO;
using OkTools.Unity;
using Spreads.Buffers;
using UnityEngine;
using YamlDotNet.Serialization;

static partial class Commands
{
    public const string DocUsageAssetDb =
@"Usage:
  okunity assetdb tables [PROJECT]
  okunity assetdb dump [PROJECT] [OUTDIR]

Commands:
  tables  List all tables in the asset databases
  dump    Dump all tables in the asset databases as CSV files in OUTDIR.

Arguments:
  OUTDIR   Location of dumped files. Defaults to project root. Any existing files will be overwritten.

  PROJECT  Path to a Unity project. defaults to the current directory. If given a subdir of a project, the project root will automatically be used.
";

    public static CliExitCode RunAssetDb(CommandContext context)
    {
        NPath projectRoot;

        // scope
        {
            var path = context.CommandLine["PROJECT"].ToString();
            if (!path.Any())
                path = NPath.CurrentDirectory;

            var project = UnityProject.TryCreateFromProjectTree(path);
            if (project == null)
                throw new DocoptInputErrorException($"Could not find a Unity project at '{path}'");

            projectRoot = project.Path;
        }

        if (context.CommandLine["tables"].IsTrue)
        {
            var serializer = new Serializer();

            {
                using var sourceAssetDb = new SourceAssetLmdb(projectRoot);
                Console.WriteLine(sourceAssetDb.DbPath);
                Console.WriteLine(serializer.Serialize(sourceAssetDb.GetInfo()));
            }
            {
                using var artifactDb = new ArtifactLmdb(projectRoot);
                Console.WriteLine(artifactDb.DbPath);
                Console.WriteLine(serializer.Serialize(artifactDb.GetInfo()));
            }

            return CliExitCode.Success;
        }

        if (context.CommandLine["dump"].IsTrue)
        {
            NPath outDir;

            {
                var path = context.CommandLine["OUTDIR"].ToString();
                if (!path.Any())
                    path = projectRoot;

                outDir = path.ToNPath();
                if (!outDir.DirectoryExists())
                    throw new DocoptInputErrorException($"OUTDIR does not exist: '{outDir}'");
            }

            SourceAssetDbToCsv(projectRoot, outDir);
            return CliExitCode.Success;
        }

        // shouldn't get here
        return CliExitCode.ErrorUsage;
    }

    static void SourceAssetDbToCsv(NPath projectRoot, NPath outDir)
    {
        using var sourceAssetDb = new SourceAssetLmdb(projectRoot);

        static string FromStringArray(DirectBuffer value)
        {
            if (value.IsEmpty)
                return "";

            // LMDBHelpers.h line 86

            return "ARRAY"; // $$$$ TODO
        }

        static string FromString(DirectBuffer value)
        {
            return Encoding.ASCII.GetString(value.Span[..^1]);
        }

        static string FromSInt32(DirectBuffer value)
        {
            var i = value.Read<Int32>(0);
            return i.ToString();
        }

        static string FromSInt32Pair(DirectBuffer value)
        {
            var i0 = value.Read<Int32>(0);
            var i1 = value.Read<Int32>(4);
            return $"{i0},{i1}";
        }

        static string FromSInt64(DirectBuffer value)
        {
            var i = value.Read<Int64>(0);
            return i.ToString();
        }

        static string FromImporterID(DirectBuffer value)
        {
            var id = value.Read<ImporterID>(0);
            return $"{id.nativeImporterType},{id.scriptedImporterType}";
        }

        static string FromHash128(DirectBuffer value)
        {
            return value.Read<Hash128>(0).ToString();
        }

        var propertyDefs = new (string prefix, bool isInMetaFile, Func<DirectBuffer,string> converter)[]
        {
            /*StringArrayPropertyDefinition kLabelsPropDef*/                     ("labels",                          true,  FromStringArray),
            /*SInt32PropertyDefinition kAssetBundleIndexPropDef*/                ("AssetBundleIndex",                false, FromSInt32),
            /*StringPropertyDefinition kAssetBundleNamePropDef*/                 ("assetBundleName",                 true,  FromString),
            /*StringPropertyDefinition kAssetBundleVariantPropDef*/              ("assetBundleVariant",              true,  FromString),
            /*SInt64PropertyDefinition kMainObjectLocalIdentifierInFilePropDef*/ ("MainObjectLocalIdentifierInFile", false, FromSInt64),
            /*ImporterIDPropertyDefinition kImporterOverridePropDef*/            ("importerOverride",                true,  FromImporterID),
            /*Hash128PropertyDefinition kImportLogFilePropDef*/                  ("importLogFile",                   false, FromHash128),
            /*SInt32PairPropertyDefinition kImportLogEntriesCountPropDef*/       ("ImportLogEntriesCount",           false, FromSInt32Pair),
            /*StringPropertyDefinition kScriptCompilationAssetPathPropDef*/      ("scriptCompilationAssetPath",      false, FromString),
            /*Hash128PropertyDefinition kImporterErrorFilePropDef*/              ("importerErrorFile",               false, FromHash128),
            /*SInt32PropertyDefinition kAssetOriginProductIdPropDef*/            ("productId",                       true,  FromSInt32),
            /*StringPropertyDefinition kAssetOriginPackageNamePropDef*/          ("packageName",                     true,  FromString),
            /*StringPropertyDefinition kAssetOriginPackageVersionPropDef*/       ("packageVersion",                  true,  FromString),
            /*StringPropertyDefinition kAssetOriginAssetPathPropDef*/            ("assetPath",                       true,  FromString),
            /*SInt32PropertyDefinition kAssetOriginUploadIdPropDef*/             ("uploadId",                        true,  FromSInt32),
        }
        .Select(item =>
        {
            var prefixBuffer = new DirectBuffer(sourceAssetDb.StringToBuffer(item.prefix).Span[..^1]);
            return (item.prefix, prefixBuffer, item.isInMetaFile, item.converter);
        })
        .ToArray();

        using (var table = new GuidPropertyIdToPropertyTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.Write("Property,IsInMetaFile,UnityGUID,Value0,Value1,...\n");

            using var tx = sourceAssetDb.Env.BeginReadOnlyTransaction();
            foreach (var (key, value) in table.Table.AsEnumerable(tx))
            {
                var found = false;
                foreach (var propertyDef in propertyDefs)
                {
                    if (!key.Span.StartsWith(propertyDef.prefixBuffer.Span))
                        continue;

                    found = true;
                    var unityGuid = key.Read<UnityGUID>(propertyDef.prefixBuffer.Length);

                    csv.Write($"{propertyDef.prefix},{propertyDef.isInMetaFile},{unityGuid}");

                    var stringValue = propertyDef.converter(value);
                    if (stringValue.Length != 0)
                        csv.Write($",{stringValue}");

                    csv.Write('\n');
                }

                if (!found)
                    throw new InvalidDataException($"Unknown property: {Encoding.ASCII.GetString(key.Span)}");
            }
        }

        using (var table = new GuidToChildrenTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.Write("Parent,Hash,Child0,Child1,...\n");

            using var tx = sourceAssetDb.Env.BeginReadOnlyTransaction();
            foreach (var (parent, guidChildren) in table.SelectAll(tx))
            {
                csv.Write($"{parent},{guidChildren.hash}");
                foreach (var child in guidChildren.guids)
                    csv.Write($",{child}");
                csv.Write('\n');
            }
        }

        using (var table = new GuidToIsDirTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.Write("Guid,IsDir\n");

            using var tx = sourceAssetDb.Env.BeginReadOnlyTransaction();
            foreach (var (guid, isDir) in table.SelectAll(tx))
                csv.Write($"{guid},{isDir}\n");
        }

        using (var table = new GuidToPathTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.Write("Guid,Path\n");

            using var tx = sourceAssetDb.Env.BeginReadOnlyTransaction();
            foreach (var (guid, path) in table.SelectAll(tx))
                csv.Write($"{guid},{path}\n");
        }

        using (var table = new PathToHashTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.Write("Path,Hash,Time,FileSize,IsUntrusted\n");

            using var tx = sourceAssetDb.Env.BeginReadOnlyTransaction();
            foreach (var (path, value) in table.SelectAll(tx))
                csv.Write($"{path},{value.hash},{new DateTime(value.time)},{value.fileSize},{value.isUntrusted}\n");
        }

        // TODO: Misc

        using (var table = new PathToGuidTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.Write("Path,Guid,MetaFileHash,AssetFileHash\n");

            using var tx = sourceAssetDb.Env.BeginReadOnlyTransaction();
            foreach (var (path, value) in table.SelectAll(tx))
                csv.Write($"{path},{value.guid},{value.metaFileHash},{value.assetFileHash}\n");
        }

        using (var table = new PropertyIdToTypeTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.Write("PropertyId,Type\n");

            using var tx = sourceAssetDb.Env.BeginReadOnlyTransaction();
            foreach (var (propertyId, type) in table.SelectAll(tx))
                csv.Write($"{propertyId},{type}\n");
        }

        // TODO: RootFolders
    }
}
