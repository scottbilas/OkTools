using System.Diagnostics;
using System.Runtime.InteropServices;
using PInvoke;

class ProcessUtilsTests
{
    [Test]
    public void XGetProcessCurrentDirectory_WithValidProcessId_ReturnsCurrentDirectory()
    {
        // we're using npaths to normalize trailing slashes

        var expected = Directory.GetCurrentDirectory().ToNPath();
        var actual0 = ProcessUtils.GetProcessCurrentDirectory(Environment.ProcessId).ToNPath();
        var actual1 = ProcessUtils.SafeGetProcessCurrentDirectory(Environment.ProcessId)!.ToNPath();

        actual0.ShouldBe(expected);
        actual1.ShouldBe(expected);
    }

    [Test]
    public void XGetProcessCommandLine_WithValidProcessId_ReturnsCommandLine()
    {
        string expected;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Marshal.PtrToStringAuto(GetCommandLine());
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            expected = Process
        }

        var actual0 = ProcessUtils.GetProcessCommandLine(Environment.ProcessId);
        var actual1 = ProcessUtils.SafeGetProcessCommandLine(Environment.ProcessId);

        actual0.ShouldBe(expected);
        actual1.ShouldBe(expected);
    }

    [TestCase(0), TestCase(int.MaxValue)]
    public void GetProcessX_WithInvalidProcessId_ThrowsInvalidParameter(int pid)
    {
        Should.Throw<Win32Exception>(() => ProcessUtils.GetProcessCurrentDirectory(pid))
            .NativeErrorCode.ShouldBe(Win32ErrorCode.ERROR_INVALID_PARAMETER);
        Should.Throw<Win32Exception>(() => ProcessUtils.GetProcessCommandLine(pid))
            .NativeErrorCode.ShouldBe(Win32ErrorCode.ERROR_INVALID_PARAMETER);
    }

    [Test]
    public void GetProcessX_WithSystemProcessId_ThrowsAccessDenied()
    {
        using var process = Process.GetProcessesByName("csrss")[0];

        Should.Throw<Win32Exception>(() => ProcessUtils.GetProcessCurrentDirectory(process.Id))
            .NativeErrorCode.ShouldBe(Win32ErrorCode.ERROR_ACCESS_DENIED);
        Should.Throw<Win32Exception>(() => ProcessUtils.GetProcessCommandLine(process.Id))
            .NativeErrorCode.ShouldBe(Win32ErrorCode.ERROR_ACCESS_DENIED);
    }

    [TestCase(0), TestCase(int.MaxValue)]
    public void SafeGetProcessX_WithInvalidProcessId_ReturnsNull(int pid)
    {
        ProcessUtils.SafeGetProcessCurrentDirectory(pid).ShouldBeNull();
        ProcessUtils.SafeGetProcessCommandLine(pid).ShouldBeNull();
    }

    [Test]
    public void SafeGetProcessX_WithSystemProcessId_ReturnsNull()
    {
        using var process = Process.GetProcessesByName("csrss")[0];

        ProcessUtils.SafeGetProcessCurrentDirectory(process.Id).ShouldBeNull();
        ProcessUtils.SafeGetProcessCommandLine(process.Id).ShouldBeNull();
    }
}
