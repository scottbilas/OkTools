using System.Runtime.InteropServices;
using PInvoke;

namespace OkTools.Native;

[PublicAPI]
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
        catch (NTStatusException) { return null; }
    }

    public static string? SafeGetProcessCommandLine(int processId)
    {
        try { return GetProcessCommandLine(processId); }
        catch (Win32Exception) { return null; }
        catch (NTStatusException) { return null; }
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

        var handle = Kernel32.OpenProcess(Kernel32.ProcessAccess.PROCESS_QUERY_INFORMATION | Kernel32.ProcessAccess.PROCESS_VM_READ, false, processId);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            var processBasicInformation = new NTDll.PROCESS_BASIC_INFORMATION();
            NTDll.NtQueryInformationProcess(handle, NTDll.PROCESSINFOCLASS.ProcessBasicInformation,
                &processBasicInformation, Marshal.SizeOf(processBasicInformation), out _).ThrowOnError();

            byte* processParametersPtr;
            if (!Kernel32.ReadProcessMemory(handle,
                    (byte*)processBasicInformation.PebBaseAddress + k_processParametersOffset,
                    &processParametersPtr, new UIntPtr((uint)sizeof(byte*)), out _))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var processParameterUStr = new NTDll.UNICODE_STRING();
            if (!Kernel32.ReadProcessMemory(handle,
                    processParametersPtr + offset, &processParameterUStr, new UIntPtr((uint)Marshal.SizeOf(processParameterUStr)), out _))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            if (processParameterUStr.Buffer == null || processParameterUStr.Length == 0)
                return "";

            var processParameterStr = new string('\0', processParameterUStr.Length / 2);
            fixed (char* strBuffer = processParameterStr)
            {
                if (!Kernel32.ReadProcessMemory(handle,
                        processParameterUStr.Buffer, strBuffer, new UIntPtr(processParameterUStr.Length), out _))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return processParameterStr;
        }
        finally
        {
            handle.Close();
        }
    }
}
