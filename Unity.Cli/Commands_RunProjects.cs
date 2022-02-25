using OkTools.Core;

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
