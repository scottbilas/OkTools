using System.Runtime.InteropServices;

namespace OkTools.Core;

// only care about these right now
public enum SysPlatform : byte { Windows, Mac, Linux, }
public enum SysArchitecture : byte { X64, Arm64 }

public static class Sys
{
    public static readonly SysPlatform Platform
        = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? SysPlatform.Windows
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)     ? SysPlatform.Mac
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)   ? SysPlatform.Linux
        : throw new NotSupportedException("Unsupported/invalid platform");

    // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
    public static readonly SysArchitecture Architecture
        = RuntimeInformation.OSArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64   => SysArchitecture.X64,
            System.Runtime.InteropServices.Architecture.Arm64 => SysArchitecture.Arm64,
            _ => throw new NotSupportedException("Unsupported/invalid architecture")
        };
}
