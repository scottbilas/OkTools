const string programVersion = "0.1";

// fine to use console directly here, it's outside of any tui work
#pragma warning disable RS0030

// TODO: add --config prefix before command

var debugMode = false;

try
{
    return (int)await FlogCliArguments.CreateParser().RunCli(
        args,
        programVersion,
        FlogCliArguments.Help,
        FlogCliArguments.Usage,
        cliArgs =>
        {
            debugMode = cliArgs.OptDebug;

            using var flogApp = new FlogApp(cliArgs);
            return flogApp.Run();
        });
}
catch (CliExitException x)
{
    if (x.InnerException != null)
    {
        Console.Error.WriteLine(x.Message);
        if (debugMode)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(x.InnerException);
        }
    }
    else
        Console.Error.WriteLine(x);

    return (int)x.Code;
}
