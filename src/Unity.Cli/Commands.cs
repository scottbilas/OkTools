using System.Diagnostics.CodeAnalysis;
using DocoptNet;
using DotNetConfig;
using NiceIO;

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

    // TODO: have the command and subcommand name passed in also and make this scoped, otherwise we will have global option name
    // collisions across commands.

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

    public NPath? GetConfigPath(string variable) =>
        TryGetConfigPath(variable, out var result) ? result : null;

    public bool TryGetConfigPath(string variable, [NotNullWhen(true)] out NPath? result)
    {

        if (!TryGetConfigString(variable, out var resultString))
        {
            result = null;
            return false;
        }

        result = resultString;
        return true;
    }
}

static partial class Commands { }
