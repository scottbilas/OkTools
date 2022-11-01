using DocoptNet;
using NiceIO;
using OkTools.Unity;
using YamlDotNet.Serialization;

static partial class Commands
{
    public const string DocUsageAssetDbs =
@"Usage:
  okunity assetdbs tables [PROJECT]
  okunity assetdbs dump [PROJECT] [OUTDIR]

Commands:
  tables  List all tables in the asset databases
  dump    Dump all tables in the asset databases as CSV files in OUTDIR.

Arguments:
  OUTDIR   Location of dumped files. Defaults to project root. Any existing files will be overwritten.

  PROJECT  Path to a Unity project. defaults to the current directory. If given a subdir of a project, the project root will automatically be used.
";

    public static CliExitCode RunAssetDbs(CommandContext context)
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

            Dump(projectRoot, outDir);
            return CliExitCode.Success;
        }

        // shouldn't get here
        return CliExitCode.ErrorUsage;
    }

    static void Dump(NPath projectRoot, NPath outDir)
    {
        using var sourceAssetDb = new SourceAssetLmdb(projectRoot);

        using (var table = new PathToGuidTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.WriteLine("Path,Guid,MetaFileHash,AssetFileHash");

            using var tx = sourceAssetDb.Env.BeginReadOnlyTransaction();
            foreach (var (path, value) in table.SelectAll(tx))
            {
                csv.WriteLine($"{path},{value.guid},{value.metaFileHash},{value.assetFileHash}");
            }
        }

        using (var table = new GuidToPathTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.WriteLine("Guid,Path");

            using var tx = sourceAssetDb.Env.BeginReadOnlyTransaction();
            foreach (var (guid, path) in table.SelectAll(tx))
            {
                csv.WriteLine($"{guid},{path}");
            }
        }

        using (var table = new GuidToIsDirTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.WriteLine("Guid,IsDir");

            using var tx = sourceAssetDb.Env.BeginReadOnlyTransaction();
            foreach (var (guid, isDir) in table.SelectAll(tx))
            {
                csv.WriteLine($"{guid},{isDir}");
            }
        }

        using (var table = new PathToHashTable(sourceAssetDb))
        using (var csv = File.CreateText(outDir.Combine($"{sourceAssetDb.Name}-{table.Name}.csv")))
        {
            csv.WriteLine("Path,Hash,Time,FileSize,IsUntrusted");

            using var tx = sourceAssetDb.Env.BeginReadOnlyTransaction();
            foreach (var (path, value) in table.SelectAll(tx))
            {
                // new version... csv.WriteLine($"{path},{value.hash},{new DateTime(value.time)},{value.fileSize:X},{value.isUntrusted}");
                csv.WriteLine($"{path},{value.hash},{new DateTime(value.time)},{value.isUntrusted}");
            }
        }
    }
}
