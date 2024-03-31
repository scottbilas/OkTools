using Spectre.Console;

const string programVersion = "0.1";

var (exitCode, opts) = StaleCliArguments.CreateParser().Parse(args, programVersion, StaleCliArguments.Help, StaleCliArguments.Usage);
if (exitCode != null)
    return (int)exitCode.Value;

try
{
    using var staleApp = new StaleApp(opts);
    return (int)await staleApp.Run();
}
catch (Exception x)
{
    AnsiConsole.WriteException(x, ExceptionFormats.ShortenTypes | ExceptionFormats.ShortenPaths);
    return (int)CliExitCode.ErrorSoftware;
}
