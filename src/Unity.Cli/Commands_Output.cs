using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using OkTools.Core;
using OkTools.Unity;
using Spectre.Console;
using YamlDotNet.Serialization.NamingConventions;
using YamlSerializerBuilder = YamlDotNet.Serialization.SerializerBuilder;

static partial class Commands
{
    static void OutputJson(object thing, TextWriter where)
    {
        // json expects a newline, so use WriteLine() here
        where.WriteLine(JsonSerializer.Serialize(thing, new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            Converters = { new JsonStringEnumConverter() }
        }));
    }

    static void OutputYaml(object thing, TextWriter where)
    {
        // yaml writes its own newline, so use Write() here
        where.Write(new YamlSerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build()
            .Serialize(thing));
    }

    [Flags]
    enum OutputFlags
    {
        Json = 1 << 0, Yaml = 1 << 1, Detail = 1 << 2, Debug = 1 << 3,
    }

    static void Output(object thingObject, OutputFlags flags, TextWriter? where = null)
    {
        where ??= Console.Out;

        var json = (flags & OutputFlags.Json) != 0;
        var yaml = (flags & OutputFlags.Yaml) != 0;

        var level = StructuredOutputLevel.Flat;
        if (json || yaml)
            level = (flags & OutputFlags.Detail) != 0 ? StructuredOutputLevel.Detailed : StructuredOutputLevel.Normal;

        var things = (thingObject is IEnumerable e ? e.Flatten() : thingObject.WrapInEnumerable())
            .Select(t => t is IStructuredOutput so ? so.Output(level, (flags & OutputFlags.Debug) != 0) : t)
            .ToArray();

        if (json)
            OutputJson(things, where);

        if (yaml)
            OutputYaml(things, where);

        if (!json && !yaml)
            OutputFlat(things, where);
    }

    static void Output(object thingObject, CommandContext context, TextWriter? where = null)
    {
        var flags = default(OutputFlags);
        if (context.CommandLine["--json"].IsTrue)
            flags |= OutputFlags.Json;
        if (context.CommandLine["--yaml"].IsTrue)
            flags |= OutputFlags.Yaml;
        if (context.CommandLine["--detailed"].IsTrue)
            flags |= OutputFlags.Detail;
        if (context.Debug)
            flags |= OutputFlags.Debug;

        Output(thingObject, flags, where);
    }

    static void OutputFlat(IReadOnlyList<object> things, TextWriter where)
    {
        var unique = new HashSet<string>();
        var fields = new List<(string name, string display, Type type)>();

        // TODO: find a better/safer way to annotate structured output for better pretty printing

        foreach (var thing in things)
        {
            if (thing is not IDictionary<string, object> dict)
            {
                where.WriteLine(thing);
                continue;
            }

            foreach (var (name, value) in dict)
            {
                if (!unique.Add(name))
                    continue;

                var display = value switch
                {
                    UnityEditorBuildConfig _ => "CONFIG",
                    _ => name.ToUpper(),
                };

                fields.Add((name, display, value.GetType()));
            }
        }

        var table = new Table();
        table.NoBorder();
        table.HideHeaders();

        foreach (var field in fields)
            table.AddColumn($"[underline]{field.display}[/] ");
        table.AddRow(table.Columns.Select(c => c.Header));

        foreach (var thing in things)
        {
            if (thing is not IDictionary<string, object> dict)
                continue;

            var cells = fields
                .Select(field =>
                {
                    if (!dict.TryGetValue(field.name, out var value))
                        return "";

                    string? color = null;

                    if (value is UnityEditorBuildConfig config)
                        color = config == UnityEditorBuildConfig.Debug ? "salmon1" : "aquamarine3";

                    if (field.name == "Version" && value is string versionText)
                    {
                        var version = UnityVersion.TryFromText(versionText);
                        if (version != null)
                        {
                            var year = DateTime.Now.Year;
                            if (version.Major == year)
                                color = "aquamarine3";
                            else if (version.Major < year - 2)
                                color = "salmon1";
                        }
                    }

                    return color != null ? $"[{color}]{value}[/] " : $"{value} ";
                });

            table.AddRow(cells.ToArray());
        }

        if (table.Rows.Any())
        {
            // TODO: if it's just one row, print out vertical block (?)

            AnsiConsole.Write(table);
        }
    }
}
