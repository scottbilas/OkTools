// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32.Foundation;
using Windows.Win32.System.Diagnostics.Debug;

namespace Windows.Win32
{
    partial class PInvoke
    {
        internal static WIN32_ERROR GetLastError() => (WIN32_ERROR)Marshal.GetLastWin32Error();
    }

    namespace Foundation
    {
#       if !NETSTANDARD
        [SupportedOSPlatform("windows5.1.2600")]
#       endif
        partial struct NTSTATUS
        {
            static Dictionary<NTSTATUS, string>? s_names;

            public string? TryGetName()
            {
                if (s_names == null)
                {
                    s_names = new();

                    foreach (var field in typeof(NTSTATUS)
                        .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                        .Where(f => f.Name.StartsWith("STATUS_")))
                    {
                        var status = (NTSTATUS)field.GetValue(null)!;
                        if (s_names.TryAdd(status, field.Name))
                            continue;

                        // special - this is overloaded a lot
                        if (field.Name.EndsWith("_WAIT_0"))
                            continue;
                        if (!s_names[status].EndsWith("_WAIT_0"))
                            throw new InvalidOperationException($"Dup NTSTATUS found; both {s_names[status]} and {field.Name} are 0x{status.Value:x8}");

                        s_names[status] = field.Name;
                    }

                    // special: this one is overloaded
                }

                s_names.TryGetValue(this, out var name);
                return name;
            }

            public unsafe string? TryGetMessage()
            {
                PWSTR buffer = default;
                using var module = PInvoke.LoadLibrary("ntdll.dll");

                try
                {
                    PInvoke.SetLastError(WIN32_ERROR.NO_ERROR);
                    var result = PInvoke.FormatMessage(
                        FORMAT_MESSAGE_OPTIONS.FORMAT_MESSAGE_ALLOCATE_BUFFER |
                        FORMAT_MESSAGE_OPTIONS.FORMAT_MESSAGE_FROM_SYSTEM |
                        FORMAT_MESSAGE_OPTIONS.FORMAT_MESSAGE_FROM_HMODULE |
                        FORMAT_MESSAGE_OPTIONS.FORMAT_MESSAGE_IGNORE_INSERTS,
                        module.DangerousGetHandle().ToPointer(),
                        (uint)Value,
                        0,
                        (PWSTR)(void*)&buffer, // this should be a ref
                        0,
                        null);

                    if (result == 0)
                        return null;

                    var span = new ReadOnlySpan<char>(buffer.Value, (int)result).Trim();
                    if (span.Length == 0)
                        return null;

                    // special: override boring default
                    if (span is "STATUS_SUCCESS")
                        return "The operation succeeded.";

                    if (span.StartsWith("{"))
                    {
                        // this will be something like "{Not Implmented}\nThe requested operation is not implemented.", dunno why, so skip the first line
                        var nextLine = span.IndexOf('\n');
                        if (nextLine != -1)
                            span = span[(nextLine+1)..].Trim();
                    }

                    return new string(span);
                }
                finally
                {
                    PInvoke.LocalFree(new(buffer.Value));
                }
            }

            public string ToFullString(bool includeMessage = true)
            {
                var hexCode = $"0x{Value:X8}";
                var namedCode = TryGetName();
                var statusAsString = namedCode != null && namedCode != hexCode
                    ? $"{namedCode} [{hexCode}]"
                    : hexCode;
                var insert = $"NTSTATUS {SeverityCode}: {statusAsString}";

                var message = includeMessage ? TryGetMessage() : null;
                return message != null
                    ? $"{message} ({insert})"
                    : insert;
            }
        }
    }
}
