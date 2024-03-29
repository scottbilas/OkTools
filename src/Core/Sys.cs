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
}
