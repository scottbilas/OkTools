using DocoptNet;
using DotNetConfig;

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

    public bool GetConfigBool(string section, string subsection, string variable) =>
        GetConfigBoolHelper(section, subsection, variable);
    public bool GetConfigBool(string section, string variable) =>
        GetConfigBoolHelper(section, null, variable);

    bool GetConfigBoolHelper(string section, string? subsection, string variable)
    {
        var result = false;

        if (Config.TryGetBoolean(section, subsection, variable, out var value))
            result = value;
        if (CommandLine["--" + variable].IsTrue)
            result = true;

        return result;
    }

}

static partial class Commands { }
