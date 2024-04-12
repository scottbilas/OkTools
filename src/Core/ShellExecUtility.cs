using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32.Security;
using Windows.Win32.System.JobObjects;
using static Windows.Win32.PInvoke;

namespace OkTools.Core;

public static class ShellExecUtility
{
    public static NPath? TryResolveExecutableExtension(string basePath)
    {
        var baseNPath = basePath.ToNPath();
        return k_shellCommandExtensions
            .Prepend("") // try the file on its own first
            .Select(e => baseNPath.ChangeFilename(baseNPath.FileName + e)) // don't use ChangeExtension, might be executing "hi.there" and "hi.there.cmd" exists
            .FirstOrDefault(p => p.FileExists());
    }

    public static NPath? TryResolveExecutablePath(string pathToResolve, string? pathEnv = null)
    {
        // special: a leading "./" will get collapsed when NPath parses it
        if (pathToResolve.StartsWith("./"))
            return TryResolveExecutableExtension(pathToResolve);
        // more special: extra check for windows
        if (Sys.Platform == SysPlatform.Windows && pathToResolve.StartsWith(".\\"))
            return TryResolveExecutableExtension(pathToResolve);

        // resolve home path if it's there
        var npathToResolve = pathToResolve.ToNPath().TildeExpand();

        // if it's not a bare filename, must be a relative or absolute path, so don't check PATH
        if (npathToResolve.Depth != 1)
            return TryResolveExecutableExtension(npathToResolve);

        // we're usually working with PATH
        pathEnv ??= Environment.GetEnvironmentVariable("PATH")!;

        // search path, with current dir first
        return pathEnv
            .SplitTrimRemoveEmpty()
            .Prepend(".")
            .Select(p => TryResolveExecutableExtension(p.ToNPath().Combine(npathToResolve)))
            .FirstOrDefault(p => p != null);
    }

    public static NPath ResolveExecutablePath(string path, string? pathEnv = null) =>
        TryResolveExecutablePath(path, pathEnv)
        ?? throw new FileNotFoundException($"Unable to find executable or script '{path}'", path);

    // TODO: consider using PATHEXT
    // see PowerShell\src\System.Management.Automation\engine\CommandSearcher.cs for how to generally improve this stuff
    // including looking for the execute bit on non-windows platforms. maybe could just use those dll's directly!
    // (or copy-paste-rotate, it's MIT..)

    // these are the extensions that we support in ResolveShellCommand, in order of preference (matches default PATHEXT with .ps1 at the front, which powershell does internally)
    static readonly string[] k_shellCommandExtensions = [".ps1", ".com", ".exe", ".bat", ".cmd"];

    public static (NPath exe, string[] args) ResolveShellCommand(string path) =>
        (path.ToNPath().Extension switch
        {
            "" => ResolveExecutablePath(path).Extension, // no extension will be the common case, go try to find one
            var ext => ext
        })
        .ToLowerInvariant() switch
        {
            // all of these can be CreateProcess'd directly, cmd shell is automatically used if needed
            "com" or "exe" or "bat" or "cmd" => (path, []),

            "ps1" => (
                TryResolveExecutablePath("pwsh.exe")
                ?? TryResolveExecutablePath("powershell.exe")
                ?? throw new FileNotFoundException("Unable to find PowerShell (pwsh.exe or powershell.exe) on PATH"),
                ["-File", path]),

            // TODO: we could actually look up the associated program for this extension in the registry including its launch arg pattern, and if it's a cli app, proceed

            var ext => throw new CliErrorException(CliExitCode.ErrorUsage, $"Unsupported command extension '.{ext}'")
        };

    static bool s_childProcessesSelfDestructOnParentExit;

#   if !NETSTANDARD
    [SupportedOSPlatform("windows5.1.2600")]
#   endif
    public static unsafe bool ConfigureProcessExitToAlsoKillChildProcesses()
    {
        if (s_childProcessesSelfDestructOnParentExit)
            return false;

        s_childProcessesSelfDestructOnParentExit = true;

        // below is adapted from https://www.meziantou.net/killing-all-child-processes-when-the-parent-exits-job-object.htm

        // Ensure the handle is not duplicated in the child processes.
        // This would break JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE as there
        // all child processes would have a handle for the Job Object.
        // Thus, closing the root process would not terminate all processes
        // in the hierarchy as the Job object would still be referenced by
        // other child processes.
        var securityAttributes = new SECURITY_ATTRIBUTES
        {
            // Ensure the handle is not duplicated in the child processes.
            // This would break JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE as there
            // all child processes would have a handle for the Job Object.
            // Thus, closing the root process would not terminate all processes
            // in the hierarchy as the Job object would still be referenced by
            // other child processes.
            bInheritHandle = false,
            lpSecurityDescriptor = null,
            nLength = (uint)Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
        };

        var jobHandle = CreateJobObject(securityAttributes, null);
        if (jobHandle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        // Configure the Job Object to kill all processes when the root process is killed.
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                // Kill all processes associated to the job when the last handle is closed
                LimitFlags = JOB_OBJECT_LIMIT.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
            },
        };

        if (!SetInformationJobObject(
                jobHandle,
                JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
                &info,
                (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        // Assign the Job object to the current process.
        // As we don't allow child processes to escape from the Job object in the LimitFlags using
        // JOB_OBJECT_LIMIT_BREAKAWAY_OK or JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK, all child
        // processes will be associated to this Job Object automatically.
        if (!AssignProcessToJobObject(jobHandle, Process.GetCurrentProcess().SafeHandle))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        return true;
    }
}
