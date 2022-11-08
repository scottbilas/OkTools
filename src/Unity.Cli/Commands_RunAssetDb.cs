using System.Text;
using System.Text.Json;
using DocoptNet;
using NiceIO;
using OkTools.Unity;
using OkTools.Unity.AssetDb;
using YamlDotNet.Serialization;

static partial class Commands
{
    public const string DocUsageAssetDb =
@"Usage:
  okunity assetdb tables [PROJECT]
  okunity assetdb dump [--json] [PROJECT] [OUTDIR]

Commands:
  tables  List all tables in the asset databases
  dump    Dump all tables in the asset databases in OUTDIR. Defaults to CSV format unless `--json` is used.

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

            var useCsv = context.CommandLine["--json"].IsFalse;
            SourceAssetDbToCsv(projectRoot, outDir, useCsv);
            ArtifactDbToCsv(projectRoot, outDir, useCsv);

            return CliExitCode.Success;
        }

        // shouldn't get here
        return CliExitCode.ErrorUsage;
    }

    static void SourceAssetDbToCsv(NPath projectRoot, NPath outDir, bool useCsv)
    {
        using var sourceAssetDb = new SourceAssetLmdb(projectRoot);

        foreach (var spec in SourceAssetTables.All)
        {
            var path = outDir.Combine($"{sourceAssetDb.Name}-{spec.TableName}");
            sourceAssetDb.DumpTable(spec, path, useCsv);
        }

        using (var table = new MiscTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.Write("Misc,Value0,Value1,...\n");

            using var tx = sourceAssetDb.Env.BeginReadOnlyTransaction();
            foreach (var (misc, value) in table.SelectAll(tx))
            {
                var assetBundleNames = misc.TryGetAssetBundleNames(value);
                if (assetBundleNames != null)
                {
                    for (var i = 0; i < assetBundleNames.Length; ++i)
                    {
                        var abn = assetBundleNames[i];
                        csv.Write($"{misc.Name}{i},{abn.AssetBundleName},{abn.AssetBundleVariant},{abn.Index}\n");
                    }
                }
                else
                {
                    csv.Write($"{misc.Name}");
                    var stringValue = misc.ToCsv(value);
                    if (stringValue.Length != 0)
                        csv.Write($",{stringValue}");
                    csv.Write('\n');
                }
            }
        }

        using (var table = new PathToGuidTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.Write("Path,UnityGuid,MetaFileHash,AssetFileHash\n");

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

        using (var table = new RootFoldersTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.Write("RootFolder,UnityGuid,Immutable,MountPoint,Folder,PhysicalPath\n");

            using var tx = sourceAssetDb.Env.BeginReadOnlyTransaction();
            foreach (var (folder, properties) in table.SelectAll(tx))
                csv.Write($"{folder},{properties.Guid},{properties.Immutable},{properties.MountPoint},{properties.Folder},{properties.PhysicalPath}\n");
        }
    }

    static void ArtifactDbToCsv(NPath projectRoot, NPath outDir, bool useCsv)
    {
        using var artifactDb = new ArtifactLmdb(projectRoot);

        /* NOT READY
        using (var table = new ArtifactIdPropertyIdToPropertyTable(artifactDb))
        using (var csv = File.CreateText(outDir.Combine($"{artifactDb.Name}-{table.Name}.csv")))
        {
            csv.Write("ArtifactID,Property,IsInMetaFile,Value0,Value1,...\n");

            var sb = new StringBuilder();

            using var tx = artifactDb.Env.BeginReadOnlyTransaction();
            foreach (var (id, prop, value) in table.SelectAll(tx))
            {
                csv.Write($"{id.Hash},{prop.Name},{prop.IsInMetaFile}");

                var stringValue = prop.ToCsv(value, sb);
                if (stringValue.Length != 0)
                    csv.Write($",{stringValue}");

                csv.Write('\n');
            }
        }
        */

        using (var table = new ArtifactIdToImportStatsTable(artifactDb))
        using (var csv = File.CreateText(outDir.Combine($"{artifactDb.Name}-{table.Name}.csv")))
        {
            csv.Write($"ArtifactID,{ArtifactImportStats.CsvHeader}\n");

            using var tx = artifactDb.Env.BeginReadOnlyTransaction();
            foreach (var (id, stats) in table.SelectAll(tx))
            {
                csv.Write($"{id.Hash},{stats.ToCsv()}\n");
            }
        }

        using (var table = new ArtifactKeyToArtifactIdsTable(artifactDb))
        using (var csv = File.CreateText(outDir.Combine($"{artifactDb.Name}-{table.Name}.csv")))
        {
            csv.Write($"ArtifactKeyHash,{BlobArtifactKey.CsvHeader},ArtifactId0,ArtifactId1,...\n");

            using var tx = artifactDb.Env.BeginReadOnlyTransaction();
            foreach (var (key, ids) in table.SelectAll(tx))
            {
                csv.Write($"{key},{ids.ArtifactKey.ToCsv()}");
                foreach (var id in ids.Ids)
                    csv.Write($",{id.Hash}");
                csv.Write('\n');
            }
        }

        using (var table = new CurrentRevisionsTable(artifactDb))
        using (var csv = File.CreateText(outDir.Combine($"{artifactDb.Name}-{table.Name}.csv")))
        {
            csv.Write("ArtifactKeyHash,UnityGuid,NativeImporterType,ScriptedImporterType,ArtifactId\n");

            using var tx = artifactDb.Env.BeginReadOnlyTransaction();
            foreach (var (key, rev) in table.SelectAll(tx))
                csv.Write(
                    $"{key},{rev.ArtifactKey.Guid},{rev.ArtifactKey.ImporterId.NativeImporterType},"+
                    $"{rev.ArtifactKey.ImporterId.ScriptedImporterType},{rev.ArtifactId.Hash}\n");
        }
    }
}
