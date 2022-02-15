using System.Text.Json;
using System.Text.Json.Serialization;
using DocoptNet;
using DotNetConfig;
using NiceIO;
using OkTools.Core;
using OkTools.Unity;

static class Commands
{
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
        toolchains = toolchains.DistinctBy(t => t.Path).ToList();

        Output(toolchains, opt);
    }

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
            var nthing = thing.ToNPath();
            if (nthing.DirectoryExists())
            {
                var toolchain = UnityToolchain.TryCreateFromPath(nthing);
                if (toolchain != null)
                    return toolchain;

                // is it a project folder

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
        if (opt["--json"].IsTrue)
        {
            var serializeOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                IncludeFields = true,
                Converters =
                {
                    new ToStringConverter<UnityVersion>(),
                    new ToStringConverter<Exception> { ExactTypeMatch = false },
                    new JsonStringEnumConverter()
                }
            };
            Console.WriteLine(JsonSerializer.Serialize(things, serializeOptions));
        }
        else
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
