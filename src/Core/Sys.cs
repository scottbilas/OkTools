using System.Diagnostics;
using System.Runtime.InteropServices;
#if NET
using System.Security.Principal;
#endif

namespace OkTools.Core;

// only care about these right now
public enum SysPlatform : byte { Windows, Mac, Linux, }
public enum SysArchitecture : byte { X64, Arm64 }

public static class Sys
{
#   if NET
    public static readonly SysPlatform Platform
        = OperatingSystem.IsWindows() ? SysPlatform.Windows
        : OperatingSystem.IsMacOS()   ? SysPlatform.Mac
        : OperatingSystem.IsLinux()   ? SysPlatform.Linux
        : throw new NotSupportedException("Unsupported/invalid platform");
#   else
    public static readonly SysPlatform Platform
        = Environment.OSVersion.Platform == PlatformID.Win32NT ? SysPlatform.Windows
        : Environment.OSVersion.Platform == PlatformID.MacOSX  ? SysPlatform.Mac
        : Environment.OSVersion.Platform == PlatformID.Unix    ? SysPlatform.Linux
        : throw new NotSupportedException("Unsupported/invalid platform");
#   endif

    // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
    public static readonly SysArchitecture Architecture
        = RuntimeInformation.OSArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64   => SysArchitecture.X64,
            System.Runtime.InteropServices.Architecture.Arm64 => SysArchitecture.Arm64,
            _ => throw new NotSupportedException("Unsupported/invalid architecture")
        };

#   if NET
    public static bool IsSudo() =>
        OperatingSystem.IsWindows() ? IsWindowsSudo() : IsUnixSudo();
#   endif

#   if NET
    static bool IsWindowsSudo() =>
#       pragma warning disable CA1416
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
#       pragma warning restore CA1416
#   endif

    static bool IsUnixSudo() => // works on mac too
        Process.Start(new ProcessStartInfo
        {
            FileName = "/usr/bin/id",
            Arguments = "-u",
            RedirectStandardOutput = true,
            UseShellExecute = false
        })?.StandardOutput.ReadToEnd().Trim() == "0";
}
