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
        var json = context.Options["--json"].IsTrue;
        var yaml = context.Options["--yaml"].IsTrue;

        var level = StructuredOutputLevel.Flat;
        if (json || yaml)
            level = context.Options["--detailed"].IsTrue ? StructuredOutputLevel.Detailed : StructuredOutputLevel.Normal;

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
        var allDicts = true;

        foreach (var thing in things)
        {
            if (thing is IDictionary<string, object> dict)
            {
                foreach (var (key, value) in dict)
                {
                    if (unique.Add(key))
                        fields.Add((key, value.GetType()));
                }
            }
            else
                allDicts = false;
        }

        // special: just output plain strings, no need for a table
        if (fields.Count == 0)
        {
            foreach (var thing in things)
                Console.WriteLine(thing.ToString());
            return;
        }

        if (!allDicts)
            fields.Insert(0, ("(unnamed)", typeof(string)));

        var tableBuilder = new TableBuilder(TableConfig.Simple());
        var headerFormat = new CellFormat(fontStyle:FontStyleExt.Underline);

        foreach (var (name, type) in fields)
        {
            // TODO: find a better/safer way to annotate structured output for better pretty printing

            if (type == typeof(UnityEditorBuildConfig))
            {
                tableBuilder
                    .AddColumn("CONFIG").RowFormatter<UnityEditorBuildConfig>(value =>
                        value == UnityEditorBuildConfig.Debug
                            ? value.ToString().ForegroundColor(Color.Salmon)
                            : value.ToString().ForegroundColor(Color.Aquamarine))
                    .HeaderFormat(headerFormat);
            }
            else if (name == "Version") // type is probably string
            {
                tableBuilder
                    .AddColumn(name.ToUpper()).RowFormatter<string>(value =>
                    {
                        var version = UnityVersion.TryFromText(value);
                        if (version != null)
                        {
                            var year = DateTime.Now.Year;
                            if (version.Major == year)
                                return value.ForegroundColor(Color.Aquamarine);
                            if (version.Major == year - 2)
                                return value.ForegroundColor(Color.Salmon);
                        }
                        return value;
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

        var table = tableBuilder.Build();
        table.Config.hasHeaderRow = false;

        foreach (var thing in things)
        {
            if (thing is IDictionary<string, object> dict)
            {
                var cells = fields
                    .Select(field =>
                    {
                        dict.TryGetValue(field.name, out var value);
                        return value ?? "";
                    });
                if (!allDicts)
                    cells = cells.Prepend("");
                table.AddRow(cells.ToArray());
            }
            else
                table.AddRow(thing.ToString());
        }

        Console.WriteLine(table);
    }
}
