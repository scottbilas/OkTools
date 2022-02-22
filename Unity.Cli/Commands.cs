﻿using System.Dynamic;
using DocoptNet;
using DotNetConfig;
using NiceIO;
using OkTools.Core;
using OkTools.Unity;

class CommandContext
{
    public readonly IDictionary<string, ValueObject> CommandLine;
    public readonly Config Config;
    public readonly bool Debug;

    public CommandContext(IDictionary<string, ValueObject> options, Config config, bool debug)
    {
        CommandLine = options;
        Config = config;
        Debug = debug;
    }
}

static partial class Commands
{
    public const string DocUsageUnity =
@"Usage: okunity unity [options] [PROJECT]

Description:
  Run Unity to open the given PROJECT (defaults to '.'). Uses the project version given to find the matching
  Unity toolchain; see `okunity help toolchains` for how toolchains are discovered.

Options:
  --attach-debugger   Unity will pause with a dialog and so you can attach a managed debugger
  --rider             Open Rider after the project opens in Unity
  --no-local-log      Disable local log feature; Unity will go back to using user-global Editor.log
  --no-burst          Completely disable Burst
  --verbose-upm-logs  Tell Unity Package Manager to write verbose logs

  All of these options will only apply to the new Unity session being launched.
";

    public static CliExitCode RunUnity(CommandContext context)
    {
        // get a valid unity project

        var projectPath = new NPath(context.CommandLine["PROJECT"].Value?.ToString() ?? ".");
        if (!projectPath.DirectoryExists())
        {
            Console.Error.WriteLine($"Could not find directory '{projectPath}'");
            return CliExitCode.ErrorNoInput;
        }

        var project = UnityProject.TryCreateFromProjectRoot(projectPath);
        if (project == null)
        {
            Console.Error.WriteLine($"Directory is not a Unity project '{projectPath}'");
            return CliExitCode.ErrorNoInput;
        }

        // find a matching toolchain

        var version = project.GetProjectUnityVersion();

        return CliExitCode.Success;
    }

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
        var toolchains = Enumerable.Empty<UnityToolchain>();

        // optionally start with defaults
        if (context.Config.GetBoolean("toolchains", null, "no-defaults") != true &&
            !context.CommandLine["--no-defaults"].IsTrue)
        {
            toolchains = toolchains
                .Concat(Unity.FindHubInstalledToolchains())
                .Concat(Unity.FindManuallyInstalledToolchains());
        }

        // add in any custom paths
        toolchains = toolchains
            .Concat(Unity.FindCustomToolchains(context.Config.GetAll("toolchains", null, "spec").Select(v => v.GetString()), false))
            .Concat(Unity.FindCustomToolchains(context.CommandLine["SPEC"].AsStrings(), true));

        // there may be dupes in the list, so filter. and we want the defaults to come first, because
        // they will have the correct origin.
        toolchains = toolchains
            .DistinctBy(t => t.Path)
            .OrderByDescending(t => t.Version); // nice to have newest stuff first

        Output(toolchains, context);
        return CliExitCode.Success;
    }

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

    public static CliExitCode DoProjects(CommandContext context)
    {
        var projects = Enumerable.Empty<UnityProject>();
/*
        projects = projects
            .Concat(Unity.FindProjects(config.GetAll("projects", null, "include").Select(v => v.GetString())))
            .Concat(Unity.FindProjects(opt["--include"].AsStrings()));

        // there may be dupes in the list, so filter. and we want the defaults to come first, because
        // they will have the correct origin.
        projects = projects
            .DistinctBy(t => t.Path)
            .OrderByDescending(t => t.Version); // nice to have newest stuff first

        Output(toolchains, opt);*/

        return CliExitCode.ErrorUnavailable;
    }

    public const string DocUsageInfo =
@"Usage: okunity info [options] [THING]...

Description:
  Extract as much unity-related info from THING(s) as possible.
  Defaults to '.' (current directory).

  Currently supported:

  - Path to a folder or file
  - A text version number

Options:
  -j, --json         Output as JSON
  -y, --yaml         Output as yaml
  -d, --detailed     Include additional info for JSON/yaml output
";

    public static CliExitCode RunInfo(CommandContext context)
    {
        var things = context.CommandLine["THING"].AsStrings().ToArray();
        if (things.Length == 0)
            things = new[] { "." };

        var outputs = new List<object>();

        foreach (var thing in things)
        {
            //if (!thing.Contains('*'))
            {
                outputs.Add(RunInfo(thing));
                //continue;
            }

            // TODO: try it as a glob
            // can't really do this without upgrading the globbing to support dir-based matching. currently the dumb
            // thing only works if you have a target filename.
            // also want to support abs or relative globbing.
        }

        Output(outputs, context);

        // TODO: what about a partial failure
        return CliExitCode.Success;
    }

    static object RunInfo(string thing)
    {
        try
        {
            var version = UnityVersion.TryFromText(thing);
            if (version != null)
                return version;

            var nthing = thing.ToNPath();
            if (nthing.DirectoryExists())
            {
                var toolchain = UnityToolchain.TryCreateFromPath(nthing);
                if (toolchain != null)
                    return toolchain;

                var project = UnityProject.TryCreateFromProjectRoot(nthing);
                if (project != null)
                    return project;

                // TODO: what else??? check git? use git to do a fast ls-files for ProjectVersion.txt and unity.exe and so on?

                // TODO: need a better way to communicate this as an error than just "return string"
                // json/yaml will want to wrap and return as a sort of header
                // flat will want to print console error
                // either will want to ensure nonzero CLI return
                return nthing.MakeAbsolute() == NPath.CurrentDirectory
                    ? "Current directory contains no Unity-related things"
                    : $"Directory contains no Unity-related things: {thing}";
            }

            if (nthing.FileExists())
            {
                if (nthing.FileName.EqualsIgnoreCase(UnityConstants.UnityExeName))
                    return UnityVersion.FromUnityExe(nthing);
                if (nthing.FileName.EqualsIgnoreCase(UnityConstants.ProjectVersionTxtFileName))
                    return UnityVersion.FromUnityProjectVersionTxt(nthing);
                if (nthing.FileName.EqualsIgnoreCase(UnityConstants.EditorsYmlFileName))
                    return UnityVersion.FromEditorsYml(nthing);

                return $"File is not a Unity-related thing: {thing}";
            }
        }
        catch (Exception x)
        {
            return new ExceptionalThing(thing, x);
        }

        return $"Unknown thing: {thing}";
    }
}

class ExceptionalThing  : IStructuredOutput
{
    readonly string _thing;
    readonly Exception _exception;

    public ExceptionalThing(string thing, Exception exception)
    {
        _thing = thing;
        _exception = exception;
    }

    public override string ToString() =>
        $"Getting info for thing '{_thing}' resulted in {_exception.GetType().Name} ({_exception.Message})";

    public object Output(StructuredOutputLevel level, bool debug)
    {
        dynamic output = new ExpandoObject();
        output.Thing = _thing;
        output.Message = _exception.Message;

        if (level >= StructuredOutputLevel.Detailed || debug)
            output.Type = _exception.GetType().FullName!;
        if (debug && _exception.StackTrace != null)
            output.StackTrace = _exception.StackTrace;

        return output;
    }
}
