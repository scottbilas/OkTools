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
}

static partial class Commands { }
