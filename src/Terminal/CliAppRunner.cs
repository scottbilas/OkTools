namespace OkTools.Terminal;

using System.Globalization;
using System.Text;
using DocoptNet;
using Terminal = Vezel.Cathode.Terminal;

public class CliAppConfig
{
    public required string ProgramVersion { get; init; }
    public required IReadOnlyList<CliCommandSpec> Commands { get; init; }

    public string ProgramName { get; init; } = CliUtility.ProgramName;

    // static name, static replacement
    public IReadOnlyList<SimpleTextMacro>? SimpleMacros { get; init; }
    // static name, dynamic replacement
    public IReadOnlyList<SimpleMacroWriter>? MacroWriters { get; init; }
    // fully dynamic processing (processed after SimpleMacros and MacroWriters)
    public TextUtility.MacroReplacer? MacroReplacer { get; init; }

    public CliCommandSpec? TryGetCommand(string name) =>
        Commands.FirstOrDefault(c => c.Name == name);
    public CliCommandSpec GetCommand(string name) =>
        TryGetCommand(name) ?? throw new DocoptInputErrorException($"Unknown command: {name}");
}

public static class CliAppRunner
{
    // do not want docopt doing any extra special stuff here for help/version, we do it fully ourselves
    public static Task<int> Run<TArgs>(CliAppConfig config, IHelpFeaturingParser<TArgs> parser, IReadOnlyList<string> cliArgs)
        where TArgs : ICliDocoptArgs => Run(config, parser.DisableHelp(), cliArgs);
    public static Task<int> Run<TArgs>(CliAppConfig config, IVersionFeaturingParser<TArgs> parser, IReadOnlyList<string> cliArgs)
        where TArgs : ICliDocoptArgs => Run(config, parser.DisableVersion(), cliArgs);
    public static Task<int> Run<TArgs>(CliAppConfig config, IBaselineParser<TArgs> parser, IReadOnlyList<string> cliArgs)
        where TArgs : ICliDocoptArgs => Run(new CliContext(config), parser, cliArgs);

    public static Task<int> Run<TContext, TArgs>(TContext ctx, IHelpFeaturingParser<TArgs> parser, IReadOnlyList<string> cliArgs)
        where TArgs : ICliDocoptArgs where TContext : CliContext => Run(ctx, parser.DisableHelp(), cliArgs);
    public static Task<int> Run<TContext, TArgs>(TContext ctx, IVersionFeaturingParser<TArgs> parser, IReadOnlyList<string> cliArgs)
        where TArgs : ICliDocoptArgs where TContext : CliContext => Run(ctx, parser.DisableVersion(), cliArgs);
    public static async Task<int> Run<TContext, TArgs>(TContext ctx, IBaselineParser<TArgs> parser, IReadOnlyList<string> cliArgs)
        where TArgs : ICliDocoptArgs where TContext : CliContext
    {
        CultureInfo.DefaultThreadCurrentCulture   = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        PauseLevel pauseLevel = default;

        try
        {
            // catch this likely-common one
            if (cliArgs is ["--help" or "-?"])
                cliArgs = ["help"];

            // try any non-command variants first
            if (parser.Parse(cliArgs) is IArgumentsResult<TArgs> { Arguments.OptVersion: true })
            {
                await ctx.Status.WriteLineAsync($"{ctx.Config.ProgramName} {ctx.Config.ProgramVersion}");
                return (int)CliExitCode.Help;
            }

            // now do it as if it has a command with preceding options
            var args = parser
                .WithOptions(ArgsParseOptions.Default.WithOptionsFirst(true))
                .ParseToArguments(cliArgs);

            // do this as early as we can
            DocoptUtils.ParseEnumOpt(args.OptPauseLevel, out pauseLevel);

            // now we have enough info to get the context set up
            DocoptUtils.ParseEnumOpt<DebugLevel>(args.OptDebugLevel, out var debugLevel);
            DocoptUtils.ParseEnumOpt<OutputFormat>(args.OptOutputFormat, out var outputFormat);
            ctx.InitLogging(debugLevel, outputFormat);
            if (ctx.IsVerbose)
            {
                await ctx.Verbose.WriteLineAsync("Command line: " + Environment.CommandLine);
                await ctx.Verbose.WriteObjectAsync(ctx, args, nameof(args));
                await ctx.Verbose.WriteObjectAsync(ctx, ctx, nameof(ctx));
            }

            // get command, defaulting to 'help' if user didn't give one
            var commandArgs = args.ArgCommandArgs
                .Prepend(args.ArgCommand ?? "help")
                .ToList();

            // rearrange this alternate way of asking for help into standard command form
            if (commandArgs.Count > 1 && commandArgs[1] is "--help" or "-?")
            {
                commandArgs[1] = commandArgs[0];
                commandArgs[0] = "help";
            }

            return (int)await ctx.Config.GetCommand(commandArgs[0]).Exec(ctx, commandArgs);
        }
        catch (DocoptInputErrorException x)
        {
            if (cliArgs.Count != 0)
                await ctx.Error.WriteLineAsync("bad command line: " + cliArgs.StringJoin(' '));

            var sb = new StringBuilder();
            if (x.Message.Length != 0)
                sb.AppendLf(x.Message);

            var usage = x.TryGetUsage();
            if (usage != null)
                sb.AppendLf().AppendLf(usage);

            var message = DocoptUtils.Reflow(ctx.ReplaceMacros(sb.ToString()).ToString(), ctx.WrapWidth ?? 0);
            await ctx.Status.WriteAsync(message);

            return (int)CliExitCode.ErrorUsage;
        }
        catch (Exception x)
        {
            if (ctx.IsVerbose)
                throw;

            switch (x)
            {
                case DirectoryNotFoundException or FileNotFoundException:
                    await ctx.Error.WriteLineAsync(x.Message);
                    return (int)CliExitCode.ErrorNoInput;

                case CliErrorException xc:
                    await ctx.Error.WriteLineAsync(xc.Message);
                    await ctx.Error.WriteLineAsync($"exiting with error code {(int)xc.Code}");
                    return (int)xc.Code;
            }

            ctx.Error.WriteException(x);

            return (int)CliExitCode.ErrorSoftware;
        }
        finally
        {
            if (pauseLevel != PauseLevel.Off)
            {
                // vezel doesn't support this yet
                if (!TerminalUtils.IsFullyInteractive)
                {
                    await ctx.Warning.WriteLineAsync("stdin/out is redirected; ignoring `--pause`");
                }
                else if (pauseLevel switch
                    {
                        PauseLevel.Always => true,
                        PauseLevel.Errors   when ctx.Error.Any => true,
                        PauseLevel.Warnings when ctx.Error.Any || ctx.Warning.Any => true,
                        _ => false,
                    })
                {
                    if (ctx.Error.Any)
                        await ctx.Error.WriteAsync("Errors occurred; press any key to exit...");
                    else if (ctx.Warning.Any)
                        await ctx.Error.WriteAsync("Warnings occurred; press any key to exit...");
                    else
                        await ctx.Status.WriteAsync("Press any key to exit...");

                    Terminal.StandardIn.TextReader.Read();
                }
            }
        }
    }
}
