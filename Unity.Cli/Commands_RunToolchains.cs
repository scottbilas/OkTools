using DotNetConfig;
using OkTools.Core;
using OkTools.Unity;

static partial class Commands
{
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
        var toolchains =
            FindAllToolchains(
                context.Config,
                context.CommandLine["--no-defaults"].IsTrue) // TODO: have the cli add an override layer to the config, then just pass in config)
            .Concat(Unity.FindCustomToolchains(context.CommandLine["SPEC"].AsStrings(), true))
            .DistinctBy(t => t.Path)            // there may be dupes in the list, so filter. and we want the defaults to come first, because they will have the correct origin.
            .OrderByDescending(t => t.Version); // nice to have newest stuff first

        Output(toolchains, context);

        return CliExitCode.Success;
    }

    static IEnumerable<UnityToolchain> FindAllToolchains(Config config, bool noDefaults)
    {
        var toolchains = Enumerable.Empty<UnityToolchain>();

        if (!noDefaults && config.GetBoolean("toolchains", null, "no-defaults") != true)
        {
            toolchains = toolchains
                .Concat(Unity.FindHubInstalledToolchains())
                .Concat(Unity.FindManuallyInstalledToolchains());
        }

        return toolchains.Concat(Unity.FindCustomToolchains(
            config.GetAll("toolchains", null, "spec").Select(v => v.GetString()), false));
    }
}
