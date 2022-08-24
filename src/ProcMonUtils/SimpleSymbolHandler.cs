using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using BetterWin32Errors;
using Zodiacon.DebugHelp;

namespace OkTools.ProcMonUtils;

// this is a cut down DebugHelp.SymbolHandler that doesn't do anything with slow exceptions
public sealed class SimpleSymbolHandler : IDisposable
{
    static int s_Serial;
    IntPtr m_Handle;

    static SimpleSymbolHandler()
    {
        // system32 has a dbghelp.dll, but we also want symsrv.dll to enable auto-download of symbols if the
        // user has _NT_SYMBOL_PATH set up to do so. i copied dbghelp.dll/symsrv.dll out of C:\Program Files (x86)\Windows Kits\10\Debuggers\x64.
        // (there's probably a better way to do this..)

        // recursive check because debug build is in a subdir of builds folder
        var dbgHelpPath = Assembly.GetExecutingAssembly().Location.ToNPath().ParentContaining("dbghelp.dll", true);
        if (dbgHelpPath != null!)
            NativeLibrary.Load(dbgHelpPath);
    }

    public SimpleSymbolHandler(string? ntSymbolPath = null)
    {
        m_Handle = new IntPtr(++s_Serial);

        var options = SymbolOptions.UndecorateNames | SymbolOptions.LoadLines;
        if (Debugger.IsAttached)
            options |= SymbolOptions.Debug;

        Win32.SymSetOptions(options);

        if (!Win32.SymInitialize(m_Handle, ntSymbolPath, false))
            throw new Win32Exception();
    }

    public void Dispose()
    {
        Win32.SymCleanup(m_Handle);
    }

    // FUTURE: check the DLL at `imageName` against a fingerprint. We don't get a checksum in the PML, but can do the
    // next best thing at least, which is check size, version, timestamp.
    public Win32Error LoadModule(string imageName, ulong dllBase = 0, bool skipSymbols = false)
    {
        var flags = skipSymbols ? 0x4u /*SLMFLAG_NO_SYMBOLS*/ : 0;

        return Win32.SymLoadModuleEx(m_Handle, IntPtr.Zero, imageName, null, dllBase, 0U, IntPtr.Zero, flags) != 0
            ? Win32Error.ERROR_SUCCESS
            : Win32Exception.GetLastWin32Error(); // note that a zero return but ERROR_SUCCESS after that is how SymLoadModuleEx signifies "already loaded"
    }

    public Win32Error GetSymbolFromAddress(ulong address, ref SymbolInfo symbol, out ulong offset)
    {
        symbol.Init();
        return Win32.SymFromAddr(m_Handle, address, out offset, ref symbol)
            ? Win32Error.ERROR_SUCCESS
            : Win32Exception.GetLastWin32Error();
    }

    // too bad DebugHelp.Win32 is internal..
    [SuppressUnmanagedCodeSecurity]
    static class Win32
    {
        [DllImport("dbghelp", SetLastError = true)]
        public static extern SymbolOptions SymSetOptions(SymbolOptions options);
        [DllImport("dbghelp", EntryPoint = "SymInitializeW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SymInitialize(IntPtr hProcess, string? searchPath, bool invadeProcess);
        [DllImport("dbghelp", SetLastError = true)]
        public static extern bool SymCleanup(IntPtr hProcess);
        [DllImport("dbghelp", EntryPoint = "SymLoadModuleExW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ulong SymLoadModuleEx(IntPtr hProcess, IntPtr hFile, string imageName, string? moduleName, ulong baseOfDll, uint dllSize, IntPtr data, uint flags);
        [DllImport("dbghelp", SetLastError = true)]
        public static extern bool SymFromAddr(IntPtr hProcess, ulong address, out ulong displacement, ref SymbolInfo symbol);
        [DllImport("dbghelp", SetLastError = true)]
        public static extern bool SymGetModuleInfo(IntPtr hProcess, ulong address, ref ModuleInfo module);
    }
}
