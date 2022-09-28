using DocoptNet;

const string programVersion = "0.1";

var rc = PmlToolCliArguments.CreateParser().Parse(args,
    programVersion, PmlToolCliArguments.Help, PmlToolCliArguments.Usage,
    opts =>
    {
        if (opts.CmdHelp || opts.OptHelp)
        {
            var extraHelp = new Dictionary<string, string>
            {
                { "bake", k_bakeExtraHelp },
                { "resolve", k_resolveExtraHelp },
                { "convert", k_convertExtraHelp }
            };

            // same as if user did `pmltool --help` or `pmltool -h`
            if (opts.ArgHelpcmd == null)
                return new HelpCommandResult(PmlToolCliArguments.Help);

            if (extraHelp.TryGetValue(opts.ArgHelpcmd, out var extraHelpText))
                return new HelpCommandResult(extraHelpText);

            var available = extraHelp.Keys
                .Ordered()
                .Select(h => $"'{h}'")
                .StringJoin(", ");
            throw new DocoptInputErrorException($"Extra help currently only available for {available} (unrecognized: '{opts.ArgHelpcmd}')");
        }

        // TODO: would like to have this stored in the cliArgs object..
        if (!new[] { "none", "all", "min", null }.Contains(opts.OptMergethreads))
            throw new DocoptInputErrorException("Unrecognized merge strategy: " + opts.OptMergethreads);

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
