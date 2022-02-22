using System.Collections;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;
using BetterConsoles.Colors.Extensions;
using BetterConsoles.Core;
using BetterConsoles.Tables.Builders;
using BetterConsoles.Tables.Configuration;
using BetterConsoles.Tables.Models;
using OkTools.Core;
using OkTools.Unity;
using YamlDotNet.Serialization.NamingConventions;
using YamlSerializerBuilder = YamlDotNet.Serialization.SerializerBuilder;

static partial class Commands
{
    static void OutputJson(IEnumerable<object> things)
    {
        Console.WriteLine(JsonSerializer.Serialize(things, new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            Converters = { new JsonStringEnumConverter() }
        }));
    }

    static void OutputYaml(IEnumerable<object> things)
    {
        Console.WriteLine(new YamlSerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build()
            .Serialize(things));
    }

    static void Output(object thingObject, CommandContext context)
    {
        var json = context.CommandLine["--json"].IsTrue;
        var yaml = context.CommandLine["--yaml"].IsTrue;

        var level = StructuredOutputLevel.Flat;
        if (json || yaml)
            level = context.CommandLine["--detailed"].IsTrue ? StructuredOutputLevel.Detailed : StructuredOutputLevel.Normal;

        var things = (thingObject is IEnumerable e ? e.Flatten() : thingObject.WrapInEnumerable())
            .Select(t => t is IStructuredOutput so ? so.Output(level, context.Debug) : t)
            .ToArray();

        if (json)
            OutputJson(things);

        if (yaml)
            OutputYaml(things);

        if (!json && !yaml)
            OutputFlat(things);
    }

    static void OutputFlat(IReadOnlyList<object> things)
    {
        var unique = new HashSet<string>();
        var fields = new List<(string name, Type type)>();

        var tableBuilder = new TableBuilder(TableConfig.Simple());
        var headerFormat = new CellFormat(fontStyle:FontStyleExt.Underline);
        var wroteStrings = false;

        foreach (var thing in things)
        {
            if (thing is not IDictionary<string, object> dict)
            {
                Console.WriteLine(thing);
                wroteStrings = true;
                continue;
            }

            foreach (var (name, value) in dict)
            {
                if (!unique.Add(name))
                    continue;

                var valueType = value.GetType();
                fields.Add((name, valueType));

                // TODO: find a better/safer way to annotate structured output for better pretty printing

                if (valueType == typeof(UnityEditorBuildConfig))
                {
                    tableBuilder
                        .AddColumn("CONFIG").RowFormatter<UnityEditorBuildConfig>(v =>
                            v == UnityEditorBuildConfig.Debug
                                ? v.ToString().ForegroundColor(Color.Salmon)
                                : v.ToString().ForegroundColor(Color.Aquamarine))
                        .HeaderFormat(headerFormat);
                }
                else if (name == "Version") // type is probably string
                {
                    tableBuilder
                        .AddColumn(name.ToUpper()).RowFormatter<string>(v =>
                        {
                            var version = UnityVersion.TryFromText(v);
                            if (version != null)
                            {
                                var year = DateTime.Now.Year;
                                if (version.Major == year)
                                    return v.ForegroundColor(Color.Aquamarine);
                                if (version.Major == year - 2)
                                    return v.ForegroundColor(Color.Salmon);
                            }
                            return v;
                        })
                        .HeaderFormat(headerFormat);
                }
                else
                {
                    tableBuilder
                        .AddColumn(name.ToUpper())
                        .HeaderFormat(headerFormat);
                }
            }
        }

        var table = tableBuilder.Build();
        table.Config.hasHeaderRow = false;
        var hasRows = false;

        foreach (var thing in things)
        {
            if (thing is not IDictionary<string, object> dict)
                continue;

            var cells = fields
                .Select(field =>
                {
                    dict.TryGetValue(field.name, out var value);
                    return value ?? "";
                });
            table.AddRow(cells.ToArray());
            hasRows = true;
        }

        if (wroteStrings && hasRows)
            Console.WriteLine();

        Console.WriteLine(table);
    }
}
