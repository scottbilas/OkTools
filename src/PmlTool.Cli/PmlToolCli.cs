using System.Diagnostics;

const string programVersion = "0.1";

var rc = PmlToolCliArguments.CreateParser().Parse(args,
    programVersion, PmlToolCliArguments.Help, PmlToolCliArguments.Usage,
    opts =>
    {
        if (opts.CmdHelp)
            return true;

        // TODO: not loving this validation going here. rethink that Parse helper..
        // also would like to have this stored in the cliArgs object..
        if (!new[] { "none", "all", "min", null }.Contains(opts.OptMergethreads))
            throw new DocoptNet.DocoptInputErrorException("Unrecognized merge strategy: " + opts.OptMergethreads);

        return false;
    })
    switch
    {
        // parser handled printing help or whatever
        ({ } exitCode, _) => exitCode,

        // the main commands
        (_, { CmdBake:    true } opts) => Bake   (opts),
        (_, { CmdResolve: true } opts) => Resolve(opts),
        (_, { CmdQuery:   true } opts) => Query  (opts),
        (_, { CmdConvert: true } opts) => Convert(opts),

        // nothing to do
        _ => CliExitCode.ErrorUsage,
    };

return (int)rc;
