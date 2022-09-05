const string programVersion = "0.1";

var (exitCode, cliOptions) = PmlToolCliArguments.CreateParser().Parse(args, programVersion,
    PmlToolCliArguments.Help, PmlToolCliArguments.Usage, cliArgs => cliArgs.CmdHelp);

if (exitCode != null)
    return (int)exitCode.Value;
if (cliOptions.CmdBake)
    return (int)Bake(cliOptions);
if (cliOptions.CmdResolve)
    return (int)Resolve(cliOptions);
if (cliOptions.CmdQuery)
    return (int)Query(cliOptions);
if (cliOptions.CmdConvert)
    return (int)Convert(cliOptions);

return 0;
