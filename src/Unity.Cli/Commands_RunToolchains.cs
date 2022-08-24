using DotNetConfig;
using OkTools.Unity;

static partial class Commands
{
    // TODO: get toolchain by partial version text match (including "just the hash")

    public const string DocUsageToolchains =
@"Usage: okunity toolchains [options] [SPEC]...

Description:
  Get info on all found toolchains, which are searched for in the following locations:

  - Paths known to Unity Hub
  - Toolchains installed with a downloadable installer
  - Glob pathspecs in config toolchains.spec (supports multi-entry)
  - Glob SPEC command line argument(s)

  Pathspecs support '*' and '**' globbing.

Options:
  -n, --no-defaults  Don't look in the default-install locations for toolchains
  -j, --json         Output as JSON
  -y, --yaml         Output as yaml
  -d, --detailed     Include additional info for JSON/yaml output
";

    public static CliExitCode RunToolchains(CommandContext context)
    {
        // TODO: have the cli add an override layer to the config, then just pass in config)
        var toolchains = FindAllToolchains(context.Config, context.CommandLine["--no-defaults"].IsTrue);

        toolchains = toolchains
            .Concat(context.CommandLine["SPEC"]
                .AsStrings()
                .SelectMany(spec => Unity.FindCustomToolchains(spec, true)))
            .MakeNice();

        Output(toolchains, context);

        return CliExitCode.Success;
    }

    static IEnumerable<UnityToolchain> FindAllToolchains(Config config, bool noDefaults)
    {
        var toolchains = Enumerable.Empty<UnityToolchain>();

        if (!noDefaults && config.GetBoolean("toolchains", "no-defaults") != true)
        {
            toolchains = toolchains
                .Concat(Unity.FindHubInstalledToolchains())
                .Concat(Unity.FindManuallyInstalledToolchains());
        }

        var installRoot = config.GetNPath("install", "root");
        if (installRoot != null)
            toolchains = toolchains.Concat(Unity.FindCustomToolchains(installRoot.Combine("*"), true));

        toolchains = toolchains.Concat(config
            .GetAllStrings("toolchains", "spec")
            .SelectMany(spec => Unity.FindCustomToolchains(spec, false)));

        return toolchains;
    }
}
