using System.Diagnostics;
using System.Runtime.InteropServices;
using PInvoke;

class NativeWindowsTests
{
    [Test]
    public void GetProcessCurrentDirectory_WithValidProcessId_ReturnsCurrentDirectory()
    {
        // we're using npaths to normalize trailing slashes

        var expected = Directory.GetCurrentDirectory().ToNPath();
        var actual = NativeWindows.GetProcessCurrentDirectory(Process.GetCurrentProcess().Id)!.ToNPath();

        actual.ShouldBe(expected);
    }

    // can't use Environment.CommandLine as it has been processed by .net and won't match
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr GetCommandLine();

    [Test]
    public void GetProcessCommandLine_WithValidProcessId_ReturnsCommandLine()
    {
        var expected = Marshal.PtrToStringAuto(GetCommandLine());
        var actual = NativeWindows.GetProcessCommandLine(Process.GetCurrentProcess().Id);

        actual.ShouldBe(expected);
    }

    [TestCase(0), TestCase(int.MaxValue)]
    public void GetProcessX_WithInvalidProcessId_ThrowsInvalidParameter(int pid)
    {
        Should.Throw<Win32Exception>(() => NativeWindows.GetProcessCurrentDirectory(pid))
            .NativeErrorCode.ShouldBe(Win32ErrorCode.ERROR_INVALID_PARAMETER);
        Should.Throw<Win32Exception>(() => NativeWindows.GetProcessCommandLine(pid))
            .NativeErrorCode.ShouldBe(Win32ErrorCode.ERROR_INVALID_PARAMETER);
    }

    [Test]
    public void GetProcessX_WithSystemProcessId_ThrowsAccessDenied()
    {
        using var process = Process.GetProcessesByName("csrss")[0];

        Should.Throw<Win32Exception>(() => NativeWindows.GetProcessCurrentDirectory(process.Id))
            .NativeErrorCode.ShouldBe(Win32ErrorCode.ERROR_ACCESS_DENIED);
        Should.Throw<Win32Exception>(() => NativeWindows.GetProcessCommandLine(process.Id))
            .NativeErrorCode.ShouldBe(Win32ErrorCode.ERROR_ACCESS_DENIED);
    }
}
