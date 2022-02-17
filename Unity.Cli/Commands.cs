using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocoptNet;
using DotNetConfig;
using NiceIO;
using OkTools.Core;
using OkTools.Unity;
using YamlDotNet.Serialization.NamingConventions;
using YamlSerializerBuilder = YamlDotNet.Serialization.SerializerBuilder;

static class Commands
{
    public const string DocUsageToolchains =
@"usage: okunity toolchains [-n] [-i SPEC]... [--json --yaml] [--detail DETAIL]

options:
  -n, --no-defaults   Don't look in the default-install locations for toolchains
  -i, --include SPEC  Add a pathspec to the search (supports '*' and '**' glob style wildcards)
  --json              Output as JSON
  --yaml              Output as yaml
  --detail DETAIL     Tune output detail; can be one of minimal, typical, full, or debug, [default: typical]
";

    public static void Toolchains(IDictionary<string, ValueObject> opt, Config config)
    {
        var toolchains = Enumerable.Empty<UnityToolchain>();

        // optionally start with defaults
        if (config.GetBoolean("toolchains", null, "no-defaults") != true &&
            !opt["--no-defaults"].IsTrue)
        {
            toolchains = toolchains
                .Concat(Unity.FindHubInstalledToolchains())
                .Concat(Unity.FindManuallyInstalledToolchains());
        }

        // add in any custom paths
        toolchains = toolchains
            .Concat(Unity.FindCustomToolchains(config.GetAll("toolchains", null, "include").Select(v => v.GetString())))
            .Concat(Unity.FindCustomToolchains(opt["--include"].AsStrings()));

        // there may be dupes in the list, so filter. and we want the defaults to come first, because
        // they will have the correct origin.
        toolchains = toolchains
            .DistinctBy(t => t.Path)
            .OrderByDescending(t => t.Version) // nice to have newest stuff first
            .ToList();

        Output(toolchains, opt);
    }

    public const string DocUsageInfo =
@"usage: okunity info [--json --yaml] [--detail DETAIL] [THING]...

Extract as much unity-related info from THING(s) as possible.
Defaults to '.' (the current directory). Currently supported:

  * Path to a folder or file
  * A text version number

options:
  --json           Output as JSON
  --yaml           Output as yaml
  --detail DETAIL  Tune output detail; can be one of minimal, typical, full, or debug, [default: typical]
";

    public static void Info(IDictionary<string, ValueObject> opt, Config config)
    {
        var things = opt["THING"].AsStrings().ToArray();
        if (things.Length == 0)
            things = new[] { "." };

        Output(things.Select(Info), opt);
    }

    static object Info(string thing)
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

                // TODO: what else???

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

    static void Output(IEnumerable<object> things, IDictionary<string, ValueObject> opt)
    {
        var json = opt["--json"].IsTrue;
        var yaml = opt["--yaml"].IsTrue;

        things = things.Flatten();

        if (json || yaml)
        {
            var detailText = opt["--detail"].ToString();
            var detail = EnumUtility.TryParseNoCaseOr<StructuredOutputDetail>(detailText)
                ?? throw new DocoptInputErrorException($"Illegal DETAIL '{detailText}'");
            things = things.Select(thing => thing is IStructuredOutput so ? so.Output(detail) : thing);
        }

        things = things.UnDefer();

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(things, new JsonSerializerOptions
            {
                WriteIndented = true,
                IncludeFields = true,
                Converters = { new JsonStringEnumConverter() }
            }));
        }

        if (yaml)
        {
            Console.WriteLine(new YamlSerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build()
                .Serialize(things));
        }

        if (!json && !yaml)
        {
            foreach (var thing in things)
                Console.WriteLine(thing);
        }
    }
}

class ExceptionalThing
{
    public readonly string Thing;
    public readonly Exception Exception;

    public ExceptionalThing(string thing, Exception exception)
    {
        Thing = thing;
        Exception = exception;
    }

    public override string ToString() =>
        $"Getting info for thing '{Thing}' resulted in {Exception.GetType().Name} ({Exception.Message})";
}

public static class StructuredOutput
{
    public static dynamic From(Exception exception, StructuredOutputDetail detail)
    {
        dynamic output = new ExpandoObject();
        output.Message = exception.Message;

        if (detail >= StructuredOutputDetail.Full)
            output.Type = exception.GetType().FullName!;
        if (detail >= StructuredOutputDetail.Debug && exception.StackTrace != null)
            output.StackTrace = exception.StackTrace;

        return output;
    }
}
