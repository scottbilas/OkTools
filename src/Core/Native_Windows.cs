using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Wdk.System.Threading;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;
using static Windows.Win32.PInvoke;
using static Windows.Wdk.PInvoke;

#if !NETSTANDARD
using System.Runtime.Versioning;
#endif

namespace OkTools.Core;

#if !NETSTANDARD
[SupportedOSPlatform("windows5.1.2600")]
#endif
public class NtStatusException : Exception
{
    internal NtStatusException(NTSTATUS status, string? message = null, Exception? inner = null)
        : base(message ?? status.ToFullString(), inner) => Status = status;

    internal NTSTATUS Status { get; }
}

#if !NETSTANDARD
[SupportedOSPlatform("windows5.1.2600")]
#endif
static class NativeWindowsExtensions
{
    public static void ThrowOnError(this NTSTATUS @this)
    {
        if (@this.SeverityCode == NTSTATUS.Severity.Error)
            throw new NtStatusException(@this);
    }

    public static HANDLE AsRawHandle(this SafeHandle @this) =>
        new(@this.DangerousGetHandle());
}

[PublicAPI]
#if !NETSTANDARD
[SupportedOSPlatform("windows5.1.2600")]
#endif
public static class NativeWindows
{
    public static string GetProcessCurrentDirectory(int processId) =>
        GetProcessParametersString(processId, k_processCurrentDirectoryOffset);
    public static string GetProcessCommandLine(int processId) =>
        GetProcessParametersString(processId, k_processCommandLineOffset);

    public static string? SafeGetProcessCurrentDirectory(int processId)
    {
        try { return GetProcessCurrentDirectory(processId); }
        catch (Win32Exception) { return null; }
        catch (NtStatusException) { return null; }
    }

    public static string? SafeGetProcessCommandLine(int processId)
    {
        try { return GetProcessCommandLine(processId); }
        catch (Win32Exception) { return null; }
        catch (NtStatusException) { return null; }
    }

    // these constants from https://stackoverflow.com/a/23842609/14582
    // internals stuff! may break in a new windows release! (unlikely! been this way for at least 10 years!)

    const uint k_processParametersOffset       = 0x20; // windbg `dt ntdll!_PEB` and grep `ProcessParameters` to get offset from base
    const uint k_processCurrentDirectoryOffset = 0x38; // undocumented
    const uint k_processCommandLineOffset      = 0x70; // offset to winternl.h:RTL_USER_PROCESS_PARAMETERS::CommandLine

    static unsafe string GetProcessParametersString(int processId, uint offset)
    {
        if (sizeof(void*) != 8)
            throw new NotSupportedException("Requires 64-bit OS");

        using var handle = OpenProcess_SafeHandle(
            PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION | PROCESS_ACCESS_RIGHTS.PROCESS_VM_READ,
            false,
            (uint)processId);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        uint dummy = 0;
        var processBasicInformation = new PROCESS_BASIC_INFORMATION();
        NtQueryInformationProcess(handle.AsRawHandle(), PROCESSINFOCLASS.ProcessBasicInformation,
            &processBasicInformation, (uint)Marshal.SizeOf(processBasicInformation), ref dummy).ThrowOnError();

        byte* processParametersPtr;
        if (!ReadProcessMemory(handle,
                (byte*)processBasicInformation.PebBaseAddress + k_processParametersOffset,
                &processParametersPtr, (nuint)sizeof(byte*), null))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        var processParameterUStr = new UNICODE_STRING();
        if (!ReadProcessMemory(handle.AsRawHandle(),
                processParametersPtr + offset, &processParameterUStr, (nuint)Marshal.SizeOf(processParameterUStr), null))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        if (processParameterUStr.Buffer == null || processParameterUStr.Length == 0)
            return "";

        var processParameterStr = new string('\0', processParameterUStr.Length / 2);
        fixed (char* strBuffer = processParameterStr)
        {
            // ReSharper disable once RedundantCast
            // ^ cast is needed for netstandard, but netcore doesn't need it
            if (!ReadProcessMemory(handle.AsRawHandle(),
                processParameterUStr.Buffer, strBuffer, (nuint)processParameterUStr.Length, null))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        return processParameterStr;
    }
}
