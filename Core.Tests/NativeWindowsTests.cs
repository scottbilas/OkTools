using System.Diagnostics;
using System.Runtime.InteropServices;
using PInvoke;

class NativeWindowsTests
{
    [Test]
    public void XGetProcessCurrentDirectory_WithValidProcessId_ReturnsCurrentDirectory()
    {
        // we're using npaths to normalize trailing slashes

        var expected = Directory.GetCurrentDirectory().ToNPath();
        var actual0 = NativeWindows.GetProcessCurrentDirectory(Process.GetCurrentProcess().Id)!.ToNPath();
        var actual1 = NativeWindows.SafeGetProcessCurrentDirectory(Process.GetCurrentProcess().Id)!.ToNPath();

        actual0.ShouldBe(expected);
        actual1.ShouldBe(expected);
    }

    // TODO: consider posh implementation.. (`get-process foo|get-member -name commandline |select -expandproperty definition`)
    /* if ($IsWindows) {
           (Get-CimInstance Win32_Process -Filter "ProcessId = $($this.Id)").CommandLine
       } elseif ($IsLinux) {
           Get-Content -LiteralPath "/proc/$($this.Id)/cmdline"
       }*/

    // can't use Environment.CommandLine as it has been processed by .net and won't match
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr GetCommandLine();

    [Test]
    public void XGetProcessCommandLine_WithValidProcessId_ReturnsCommandLine()
    {
        var expected = Marshal.PtrToStringAuto(GetCommandLine());
        var actual0 = NativeWindows.GetProcessCommandLine(Process.GetCurrentProcess().Id);
        var actual1 = NativeWindows.SafeGetProcessCommandLine(Process.GetCurrentProcess().Id);

        actual0.ShouldBe(expected);
        actual1.ShouldBe(expected);
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

    [TestCase(0), TestCase(int.MaxValue)]
    public void SafeGetProcessX_WithInvalidProcessId_ReturnsNull(int pid)
    {
        NativeWindows.SafeGetProcessCurrentDirectory(pid).ShouldBeNull();
        NativeWindows.SafeGetProcessCommandLine(pid).ShouldBeNull();
    }

    [Test]
    public void SafeGetProcessX_WithSystemProcessId_ReturnsNull()
    {
        using var process = Process.GetProcessesByName("csrss")[0];

        NativeWindows.SafeGetProcessCurrentDirectory(process.Id).ShouldBeNull();
        NativeWindows.SafeGetProcessCommandLine(process.Id).ShouldBeNull();
    }
}
