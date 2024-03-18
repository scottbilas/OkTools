using System.Runtime.InteropServices;
using System.Text;
using PInvoke;

namespace OkTools.Core;

// TODO: "safe"?? we don't want different exceptions for different plats..wrap up any exceptions?
// TODO: "try" instead?

[PublicAPI]
public static class ProcessUtils
{
    public static string? SafeGetCurrentDirectory(int processId)
    {
        if (processId == Environment.ProcessId)
            return Environment.CurrentDirectory;

        if (OperatingSystem.IsWindows())
            return Windows.SafeGetCurrentDirectory(processId);
        if (OperatingSystem.IsMacOS())
            return Mac.SafeGetCurrentDirectory(processId);
        if (OperatingSystem.IsLinux())
            return Linux.SafeGetCurrentDirectory(processId);
        throw new NotSupportedException("Not supported on this platform");
    }

    public static string? SafeGetCommandLine(int processId)
    {
        if (processId == Environment.ProcessId)
            return Environment.CommandLine;

        if (OperatingSystem.IsWindows())
            return Windows.SafeGetCommandLine(processId);
        if (OperatingSystem.IsMacOS())
            return Mac.SafeGetCommandLine(processId);
        if (OperatingSystem.IsLinux())
            return Linux.SafeGetCommandLine(processId);
        throw new NotSupportedException("Not supported on this platform");
    }

    struct Windows
    {
        // these constants from https://stackoverflow.com/a/23842609/14582
        // internals stuff! may break in a new windows release! (unlikely! been this way for at least 10 years!)

        const uint k_processParametersOffset       = 0x20; // windbg `dt ntdll!_PEB` and grep `ProcessParameters` to get offset from base
        const uint k_processCurrentDirectoryOffset = 0x38; // undocumented
        const uint k_processCommandLineOffset      = 0x70; // offset to winternl.h:RTL_USER_PROCESS_PARAMETERS::CommandLine

        public static string GetProcessCurrentDirectory(int processId) =>
            GetProcessParametersString(processId, k_processCurrentDirectoryOffset);
        public static string GetProcessCommandLine(int processId) =>
            GetProcessParametersString(processId, k_processCommandLineOffset);

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
                        &processParametersPtr, (nuint)sizeof(byte*), out _))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                var processParameterUStr = new NTDll.UNICODE_STRING();
                if (!Kernel32.ReadProcessMemory(handle,
                        processParametersPtr + offset, &processParameterUStr, (nuint)Marshal.SizeOf(processParameterUStr), out _))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                if (processParameterUStr.Buffer == null || processParameterUStr.Length == 0)
                    return "";

                var processParameterStr = new string('\0', processParameterUStr.Length / 2);
                fixed (char* strBuffer = processParameterStr)
                {
                    if (!Kernel32.ReadProcessMemory(handle,
                            processParameterUStr.Buffer, strBuffer, processParameterUStr.Length, out _))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                return processParameterStr;
            }
            finally
            {
                handle.Close();
            }
        }

        public static string SafeGetCurrentDirectory(int processId) =>
            GetProcessParametersString(processId, k_processCurrentDirectoryOffset);
            // $$$ "safe"

        public static string? SafeGetCommandLine(int processId)
        {

            GetProcessParametersString(processId, k_processCommandLineOffset);
            // $$$ "safe"
        }
    }

    struct Mac
    {
        public static string? SafeGetCurrentDirectory(int processId)
        {
            // cwd: ??
            throw new NotImplementedException();
        }

        public static string? SafeGetCommandLine(int processId)
        {
            // ps $PID -o args=
            throw new NotImplementedException();
        }

        public static string? SafeGetCommandLine() =>
            SafeGetCommandLine(Environment.ProcessId); // TODO: is there a special "self" way to get this?
    }

    struct Linux
    {
        public static string? SafeGetCurrentDirectory(int processId)
        {
            // cwd: cat /proc/$PID/cwd (probably a symlink, need resolve it with readlink)
            throw new NotImplementedException();
        }

        public static string? SafeGetCommandLine(string proc)
        {
            try
            {
                var bytes = File.ReadAllBytes($"/proc/{proc}/cmdline");
                for (var i = 0; i < bytes.Length; ++i)
                {
                    if (bytes[i] == 0)
                        bytes[i] = (byte)' ';
                }
                return Encoding.UTF8.GetString(bytes);
            }
            catch (IOException)
            {
                return null;
            }
        }

        public static string? SafeGetCommandLine(int processId) =>
            SafeGetCommandLine(processId.ToString());
        public static string? SafeGetCommandLine() =>
            SafeGetCommandLine("self");
    }
}
