static partial class Commands
{
    public const string DocUsageMonorepo =
@"Usage:
  okunity mr apply [options] [FEATURE...]

Description:
  Update git sparse checkout by enabling each selected FEATURE in `mr.yaml`.

Options:
  --config CONFIG  The path to the monorepo config file; if filename-only, will search up to the repo root [default: mr.yaml]
  --show-spec      Print the spec that would be used to update the sparse checkout
  -n --dry-run     Don't actually do anything, just print what would be done
";

    public static CliExitCode RunMonorepo(CommandContext context)
    {
        if (context.CommandLine["apply"].IsTrue)
        {
            //var config = context.GetConfigString("config");
        }

        // shouldn't get here
        return CliExitCode.ErrorUsage;
    }
}

class MonorepoSpec
{
}
