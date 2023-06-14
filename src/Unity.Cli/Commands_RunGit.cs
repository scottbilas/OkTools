static partial class Commands
{
    public const string DocUsageGit =
@"Usage:
  okunity git sparse

Commands:
  sparse  Update checkout by updating sparse spec from config
";

    public static CliExitCode RunGit(CommandContext context)
    {
        if (context.CommandLine["sparse"].IsTrue)
        {
        }

        // shouldn't get here
        return CliExitCode.ErrorUsage;
    }
}

class MonoRepoSpec
{
}
