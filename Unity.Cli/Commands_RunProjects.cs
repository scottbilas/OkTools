using OkTools.Core;

// TODO: command that does a join with toolchains to find all unused or long-since-last-used toolchains, for when
// wanting to free up disk space. also would be useful to see a little grid of what toolchain versions are used by
// what projects (including editors.yml).

static partial class Commands
{
    public const string DocUsageProjects =
@"Usage: okunity projects [options] [SPEC]...

Description:
  Get info on all projects found at SPEC (supports '*' and '**' globbing).
  If no SPEC is given, it defaults to the current directory.

Options:
  -j, --json         Output as JSON
  -y, --yaml         Output as yaml
  -d, --detailed     Include additional info for JSON/yaml output
";

    public static CliExitCode RunProjects(CommandContext context)
    {
/*
        var projects = Enumerable.Empty<UnityProject>();
        projects = projects
            .Concat(Unity.FindProjects(config.GetAllStrings("projects", "include")))
            .Concat(Unity.FindProjects(opt["--include"].AsStrings()));

        // there may be dupes in the list, so filter. and we want the defaults to come first, because
        // they will have the correct origin.
        projects = projects
            .DistinctBy(t => t.Path)
            .OrderByDescending(t => t.Version); // nice to have newest stuff first

        Output(toolchains, opt);*/

        Console.Error.WriteLine("NOT IMPLEMENTED YET");
        return CliExitCode.ErrorUnavailable;
    }
}
