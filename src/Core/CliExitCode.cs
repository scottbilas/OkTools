namespace OkTools.Core;

/// <summary>
/// Mnemonics for unixy process exit codes.
/// <list type="bullet">
///   <item>sysexit codes: https://man.openbsd.org/sysexits</item>
///   <item>bash codes: https://tldp.org/LDP/abs/html/exitcodes.html</item>
/// </list>
/// </summary>
[PublicAPI]
public enum CliExitCode
{
    Help = 0,
    #pragma warning disable CA1069
    Success = 0,
    #pragma warning restore CA1069
    ErrorGeneral = 1,

    // Sysexit codes

    /// <summary>The command was used incorrectly, e.g., with the wrong number of arguments, a bad flag, bad syntax in a parameter, or whatever.</summary>
    ErrorUsage       = 64,
    /// <summary>The input data was incorrect in some way. This should only be used for user's data and not system files.</summary>
    ErrorDataErr     = 65,
    /// <summary>An input file (not a system file) did not exist or was not readable. This could also include errors like “No message” to a mailer (if it cared to catch it).</summary>
    ErrorNoInput     = 66,
    /// <summary>The user specified did not exist. This might be used for mail addresses or remote logins.</summary>
    ErrorNoUser      = 67,
    /// <summary>The host specified did not exist. This is used in mail addresses or network requests.</summary>
    ErrorNoHost      = 68,
    /// <summary>A service is unavailable. This can occur if a support program or file does not exist. This can also be used as a catch-all message when something you wanted to do doesn't work, but you don't know why.</summary>
    ErrorUnavailable = 69,
    /// <summary>An internal software error has been detected. This should be limited to non-operating system related errors if possible.</summary>
    ErrorSoftware    = 70,
    /// <summary>An operating system error has been detected. This is intended to be used for such things as “cannot fork”, or “cannot create pipe”. It includes things like getuid(2) returning a user that does not exist in the passwd file.</summary>
    ErrorOsErr       = 71,
    /// <summary>Some system file (e.g., /etc/passwd, /var/run/utmp) does not exist, cannot be opened, or has some sort of error (e.g., syntax error).</summary>
    ErrorOsFile      = 72,
    /// <summary>A (user specified) output file cannot be created.</summary>
    ErrorCantCreate  = 73,
    /// <summary>An error occurred while doing I/O on some file.</summary>
    ErrorIoErr       = 74,
    /// <summary>Temporary failure, indicating something that is not really an error. For example that a mailer could not create a connection, and the request should be reattempted later.</summary>
    ErrorTempFail    = 75,
    /// <summary>The remote system returned something that was “not possible” during a protocol exchange.</summary>
    ErrorProtocol    = 76,
    /// <summary>You did not have sufficient permission to perform the operation. This is not intended for file system problems, which should use EX_NOINPUT or EX_CANTCREAT, but rather for higher level permissions.</summary>
    ErrorNoPerm      = 77,
    /// <summary>Something was found in an unconfigured or misconfigured state.</summary>
    ErrorConfig      = 78,

    // Bash codes

    /// <summary>Permission problem or command is not an executable</summary>
    ErrorCommandInvokedCannotExecute = 126,
    /// <summary>Possible problem with $PATH or a typo</summary>
    ErrorCommandNotFound = 127,
//  ErrorSignal = 128, // use FromSignal below
}

[PublicAPI]
public enum UnixSignal
{
    // https://www.man7.org/linux/man-pages/man7/signal.7.html

    None = 0,
    Lost = 1,              // SIGHUP  = Hangup detected on controlling terminal or death of controlling process
    KeyboardInterrupt = 2, // SIGINT  = Interrupt from keyboard (ctrl-c)
    KeyboardQuit = 3,      // SIGQUIT = Quit from keyboard (ctrl-d)
    Abort = 6,             // SIGABRT = Abort signal from `abort()` call within the app
    Kill = 9,              // SIGKILL = Instant kill, no chance for app cleanup
    Terminate = 15,        // SIGTERM = Termination signal, the "polite" kill, gives chance for cleanup (or ignored)
}

[PublicAPI]
public static class UnixSignalUtils
{
    public static CliExitCode FromSignal(int signal)
    {
        if (signal is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(signal), signal, $"Out of range 1 <= {signal} <= 31");

        return (CliExitCode)(128 + signal);
    }

    public static CliExitCode AsCliExitCode(this UnixSignal @this) => FromSignal((int)@this);
}

public class CliErrorException : Exception
{
    public CliErrorException(CliExitCode code, string message, Exception innerException)
        : base(message, innerException) => Code = code;
    public CliErrorException(CliExitCode code, string message)
        : base(message) => Code = code;

    public CliExitCode Code { get; }
}
