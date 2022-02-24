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
    static void OutputJson(IEnumerable<object> things, TextWriter where)
    {
        // json expects a newline, so use WriteLine() here
        where.WriteLine(JsonSerializer.Serialize(things, new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            Converters = { new JsonStringEnumConverter() }
        }));
    }

    static void OutputYaml(IEnumerable<object> things, TextWriter where)
    {
        // yaml writes its own newline, so use Write() here
        where.Write(new YamlSerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build()
            .Serialize(things));
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
            flags |= OutputFlags.Debug;

        Output(thingObject, flags, where);
    }

    static void OutputFlat(IReadOnlyList<object> things, TextWriter where)
    {
        var unique = new HashSet<string>();
        var fields = new List<(string name, Type type)>();

        var tableBuilder = new TableBuilder(TableConfig.Simple());
        var headerFormat = new CellFormat(fontStyle:FontStyleExt.Underline);

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
        }

        if (table.Rows.Any())
        {
            // TODO: if it's just one row, print out vertical block (?)

            where.WriteLine();
            where.WriteLine(table.ToString().TrimEnd());
        }
    }
}
