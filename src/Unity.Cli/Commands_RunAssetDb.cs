﻿using System.Text;
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
            ArtifactDbToCsv(projectRoot, outDir);
            return CliExitCode.Success;
        }

        // shouldn't get here
        return CliExitCode.ErrorUsage;
    }

    static void SourceAssetDbToCsv(NPath projectRoot, NPath outDir)
    {
        using var sourceAssetDb = new SourceAssetLmdb(projectRoot);

        using (var table = new GuidPropertyIdToPropertyTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.Write("Property,IsInMetaFile,UnityGuid,Value0,Value1,...\n");

            var sb = new StringBuilder();

            using var tx = sourceAssetDb.Env.BeginReadOnlyTransaction();
            foreach (var (guid, prop, value) in table.SelectAll(tx))
            {
                csv.Write($"{prop.Prefix},{prop.IsInMetaFile},{guid}");

                var stringValue = prop.ToCsv(value, sb);
                if (stringValue.Length != 0)
                    csv.Write($",{stringValue}");

                csv.Write('\n');
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
            csv.Write("UnityGuid,IsDir\n");

            using var tx = sourceAssetDb.Env.BeginReadOnlyTransaction();
            foreach (var (guid, isDir) in table.SelectAll(tx))
                csv.Write($"{guid},{isDir}\n");
        }

        using (var table = new GuidToPathTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.Write("UnityGuid,Path\n");

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

    static void ArtifactDbToCsv(NPath projectRoot, NPath outDir)
    {
        using var artifactDb = new ArtifactLmdb(projectRoot);

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
