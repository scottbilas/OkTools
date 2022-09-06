const string programVersion = "0.1";

var rc = PmlToolCliArguments.CreateParser().Parse(args,
    programVersion, PmlToolCliArguments.Help, PmlToolCliArguments.Usage,
    cliArgs => cliArgs.CmdHelp)
    switch
    {
        // the main commands
        (_, { CmdBake:    true } cliOptions) => Bake   (cliOptions),
        (_, { CmdResolve: true } cliOptions) => Resolve(cliOptions),
        (_, { CmdQuery:   true } cliOptions) => Query  (cliOptions),
        (_, { CmdConvert: true } cliOptions) => Convert(cliOptions),

        // parser handled printing help or whatever
        ({ } exitCode, _) => exitCode,

        // nothing to do
        _ => CliExitCode.ErrorUsage,
    };

return (int)rc;
