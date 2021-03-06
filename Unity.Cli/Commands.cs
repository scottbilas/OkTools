using System.Diagnostics.CodeAnalysis;
using DocoptNet;
using DotNetConfig;

class CommandContext
{
    public readonly string CommandName;
    public readonly IDictionary<string, ValueObject> CommandLine;
    public readonly Config Config;
    public readonly bool Debug;

    public CommandContext(string commandName, IDictionary<string, ValueObject> options, Config config, bool debug)
    {
        CommandName = commandName;
        CommandLine = options;
        Config = config;
        Debug = debug;
    }

    public bool GetConfigBool(string variable)
    {
        if (CommandLine["--" + variable].IsTrue)
            return true;

        Config.TryGetBoolean(CommandName, null, variable, out var result);
        return result;
    }

    public string? GetConfigString(string variable) =>
        TryGetConfigString(variable, out var result) ? result : null;

    public bool TryGetConfigString(string variable, [NotNullWhen(true)] out string? result)
    {
        if (CommandLine["--" + variable].Value is string clresult)
        {
            result = clresult;
            return true;
        }

        return Config.TryGetString(CommandName, null, variable, out result);
    }
}

static partial class Commands { }
