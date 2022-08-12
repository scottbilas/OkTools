using System.Dynamic;
using NiceIO;
using OkTools.Core;
using OkTools.Unity;

// TODO:
//
// project info:
//    running_unity_instance: pids, mono_pmips found, log file, version, etc.
// toolchain info:
//    look at contents of .unity-downloader-meta.yml for installed components etc.

static partial class Commands
{
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
            // TODO: move the bulk of this into OkTools.Unity

            var version = UnityVersion.TryFromText(thing);
            if (version != null)
                return version;

            var nthing = thing.ToNPath();
            if (nthing.DirectoryExists())
            {
                // TODO: have a way to name the table too

                var toolchain = UnityToolchain.TryCreateFromPath(nthing);
                if (toolchain != null)
                    return toolchain;

                var project = UnityProject.TryCreateFromProjectPath(nthing);
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
                if (nthing.FileName.EqualsIgnoreCase(UnityProjectConstants.ProjectVersionTxtFileName))
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
