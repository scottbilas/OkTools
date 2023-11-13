using DocoptNet;
using OkTools.Unity;
using OkTools.Unity.AssetDb;
using YamlDotNet.Serialization;

static partial class Commands
{
    public const string DocUsageAssetDb =
@"Usage:
  okunity assetdb tables [PROJECT]
  okunity assetdb dump [--limit ROWS] [PROJECT] [OUTDIR]
  okunity assetdb dump --json [--combined] [--compact] [--trim] [--limit ROWS] [PROJECT] [OUTDIR]

Commands:
  tables  List all tables in the asset databases
  dump    Dump all tables in the asset databases in OUTDIR. Defaults to CSV format unless `--json` is used.

Arguments:
  OUTDIR   Location of dumped files. Defaults to project root. Any existing files will be overwritten.

  PROJECT  Path to a Unity project. defaults to the current directory. If given a subdir of a project, the project root will automatically be used.

Options:
  --compact     Output in compact JSON with one line per table row
  --combined    Write a single combined file per database rather than a file per table
  --trim        Skip writing empty JSON objects and fields
  --limit ROWS  Limit the number of rows dumped per table
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

            void ListTables(AssetLmdb db)
            {
                Console.WriteLine(db.DbPath);
                Console.WriteLine(serializer.Serialize(db.GetInfo()));
            }

            using (var db = SourceAssetLmdb.OpenLmdb(projectRoot))
                ListTables(db);
            using (var db = ArtifactLmdb.OpenLmdb(projectRoot))
                ListTables(db);

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

            var config = new DumpConfig
            {
                OptJson = context.CommandLine["--json"].IsTrue,
                OptCompact = context.CommandLine["--compact"].IsTrue,
                OptCombined = context.CommandLine["--combined"].IsTrue,
                OptTrim = context.CommandLine["--trim"].IsTrue,
                OptLimit = context.CommandLine["--limit"].AsInt,
            };

            void DumpTables(AssetLmdb db, IEnumerable<TableDumpSpec> specs)
            {
                DumpContext? dump = null;
                try
                {
                    if (config.OptCombined)
                    {
                        dump = new(outDir.Combine(db.Name), config);
                        dump.Json!.WriteStartObject();
                    }

                    foreach (var spec in specs)
                    {
                        if (!config.OptCombined)
                        {
                            var path = outDir.Combine($"{db.Name}-{spec.TableName}");
                            dump = new DumpContext(path, config);
                        }

                        db.DumpTable(dump!, db, spec);

                        if (!config.OptCombined)
                        {
                            dump!.Dispose();
                            dump = null;
                        }
                    }

                    if (config.OptCombined)
                    {
                        dump!.Json!.WriteEndObject();
                    }
                }
                finally
                {
                    dump?.Dispose();
                }
            }

            using (var db = SourceAssetLmdb.OpenLmdb(projectRoot))
                DumpTables(db, SourceAssetLmdb.All);
            using (var db = ArtifactLmdb.OpenLmdb(projectRoot))
                DumpTables(db, ArtifactLmdb.All);

            return CliExitCode.Success;
        }

        // shouldn't get here
        return CliExitCode.ErrorUsage;
    }
}
