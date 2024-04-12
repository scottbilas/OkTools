using System.Runtime.Versioning;
using Windows.Win32.Foundation;

#if !NETSTANDARD
[SupportedOSPlatform("windows5.1.2600")]
#endif
class NativeWindowsNtStatusTests
{
    static IEnumerable<TestCaseData> BasicsValidCases()
    {
        foreach (var (status, name, message, full) in new[]
        {
            ( NTSTATUS.STATUS_SUCCESS,
                "STATUS_SUCCESS",
                "The operation succeeded.",
                "NTSTATUS Success: STATUS_SUCCESS [0x00000000]" ),
            ( NTSTATUS.STATUS_NOT_IMPLEMENTED,
                "STATUS_NOT_IMPLEMENTED",
                "The requested operation is not implemented.",
                "NTSTATUS Error: STATUS_NOT_IMPLEMENTED [0xC0000002]" ),
            ( NTSTATUS.STATUS_INVALID_PARAMETER,
                "STATUS_INVALID_PARAMETER",
                "An invalid parameter was passed to a service or function.",
                "NTSTATUS Error: STATUS_INVALID_PARAMETER [0xC000000D]" ),
            ( NTSTATUS.STATUS_DATA_ERROR,
                "STATUS_DATA_ERROR",
                "An error in reading or writing data occurred.",
                "NTSTATUS Error: STATUS_DATA_ERROR [0xC000003E]" ),
            ( NTSTATUS.STATUS_IMAGE_ALREADY_LOADED_AS_DLL,
                "STATUS_IMAGE_ALREADY_LOADED_AS_DLL",
                "Indicates that the specified image is already loaded as a DLL.",
                "NTSTATUS Error: STATUS_IMAGE_ALREADY_LOADED_AS_DLL [0xC000019D]" ),
            ( NTSTATUS.STATUS_EVENTLOG_CANT_START,
                "STATUS_EVENTLOG_CANT_START",
                "No Eventlog log file could be opened. The Eventlog service did not start.",
                "NTSTATUS Error: STATUS_EVENTLOG_CANT_START [0xC000018F]" ),
            ( NTSTATUS.STATUS_NO_SUCH_USER,
                "STATUS_NO_SUCH_USER",
                "The specified account does not exist.",
                "NTSTATUS Error: STATUS_NO_SUCH_USER [0xC0000064]" ),
        })
            yield return new TestCaseData(status, name, message, full).SetName(name);
    }

    [TestCaseSource(nameof(BasicsValidCases))]
    public void Basics_WithValid(NTSTATUS status, string name, string message, string full)
    {
        status.TryGetName().ShouldBe(name);
        status.TryGetMessage().ShouldBe(message);
        status.ToFullString(false).ShouldBe(full);
        status.ToFullString().ShouldBe($"{status.TryGetMessage()} ({status.ToFullString(false)})");
    }

    static IReadOnlyList<NTSTATUS.Severity> BasicsInvalidCases() => EnumUtility.GetValues<NTSTATUS.Severity>();

    [TestCaseSource(nameof(BasicsInvalidCases))]
    public void Basics_WithInvalid(NTSTATUS.Severity severity)
    {
        var statusCode = 0x000fbeef | ((int)severity << 30);
        var status = new NTSTATUS(statusCode);

        status.TryGetName().ShouldBeNull();
        status.TryGetMessage().ShouldBeNull();
        status.ToFullString().ShouldBe($"NTSTATUS {severity}: 0x{statusCode:X8}");
    }
}
