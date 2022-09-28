const string programVersion = "0.1";

// fine to use console directly here, it's outside of any tui work
#pragma warning disable RS0030

// TODO: add --config prefix before command

var debugMode = false;

var (exitCode, opt) = FlogCliArguments.CreateParser().Parse(args, programVersion, FlogCliArguments.Help, FlogCliArguments.Usage,
    opts =>
    {
        if (!Enum.TryParse(opts.OptWrap, true, out WrapType _))
            throw new DocoptNet.DocoptInputErrorException("Unrecognized wrap type: " + opts.OptWrap);
        return null;
    });
if (exitCode != null)
    return (int)exitCode.Value;

try
{
    debugMode = opt.OptDebug;

    using var flogApp = new FlogApp(opt);
    return (int)await flogApp.Run();
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
