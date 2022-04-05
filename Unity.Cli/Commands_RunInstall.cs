using OkTools.Core;

static partial class Commands
{
    public const string DocUsageInstall =
@"Usage: okunity install [options] SPEC [-- EXTRA...]

Description:
  Install or update a build of Unity.

  SPEC may be one of:

  - A text Unity version; partial version numbers are supported (e.g. simply 2020.3)
  - A path to a Unity project, which will use its ProjectVersion.txt to specify the version

  Any arguments given in EXTRA will be passed along to unity-downloader-cli.

Options:
  --output OUTPUT  Specify the output folder where Unity will be installed. Defaults to...$$$
";

    public static CliExitCode RunInstall(CommandContext context)
    {
        Console.Error.WriteLine("this command is WIP");
        return CliExitCode.ErrorUnavailable;
    }
}
