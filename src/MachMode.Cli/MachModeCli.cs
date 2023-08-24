const string programVersion = "0.1";

/*
 * syntax:
 *
 * foo  = set to this and turn everything else off
 * +foo = overlay this with everything else
 * ~foo = disable this one
 *
 * processing happens left to right and each can override previous (have a way to print out the rules as they are being evaluated)
 *
 * for now require that all locations and capabilities and profiles etc. are globally uniquely named so don't have to worry about scoping
 */

var (exitCode, opt) = MachModeCliArguments.CreateParser().Parse(args, programVersion, MachModeCliArguments.Help, MachModeCliArguments.Usage);
if (exitCode != null)
    return (int)exitCode.Value;

return (int)CliExitCode.Success;

